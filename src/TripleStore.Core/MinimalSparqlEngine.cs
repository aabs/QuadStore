using System;
using System.Collections.Generic;
using System.Linq;
using TripleStore.Core;
using VDS.RDF.Query;
using VDS.RDF.Query.Patterns;
using VDS.RDF.Parsing;
using VDS.RDF.Parsing.Tokens;

namespace SparqlEngine;

/// <summary>
/// Minimal SPARQL engine that uses dotNetRDF (if available) to parse queries and translates
/// basic triple patterns into QuadStore lookups with bitmap intersections.
/// If dotNetRDF is not available, you can construct queries manually via the provided API.
/// </summary>
public sealed class MinimalSparqlEngine
{
    private readonly QuadStore _store;

    public MinimalSparqlEngine(QuadStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    /// <summary>
    /// Parse and execute a simple SPARQL SELECT with a basic graph pattern using dotNetRDF.
    /// Supports simple triple patterns and GRAPH clauses to restrict to a named graph.
    /// Variables are mapped to nulls; graph variable binding is not supported.
    /// </summary>
    public IEnumerable<Dictionary<string, string>> ExecuteQuery(string sparql)
    {
        if (sparql is null) throw new ArgumentNullException(nameof(sparql));
        var parser = new SparqlQueryParser();
        SparqlQuery q = parser.ParseFromString(sparql);
        var root = q.RootGraphPattern; // BaseGraphPattern
        if (root is null) return Enumerable.Empty<Dictionary<string, string>>();

        // Collect patterns possibly scoped by GRAPH clauses
        var results = new List<Dictionary<string, string>>();

        // Execute patterns at root (default graph semantics)
        var rootPatterns = root.TriplePatterns.OfType<TriplePattern>().Select(tp => ToSPO(tp, q)).ToList();
        if (rootPatterns.Count > 0)
        {
            foreach (var row in ExecuteBasicGraphPattern(rootPatterns))
            {
                results.Add(row);
            }
        }

        // Handle GRAPH sub-patterns
        foreach (var gp in root.ChildGraphPatterns)
        {
            if (!gp.IsGraph)
            {
                // Merge non-graph child patterns into root handling
                var pats = gp.TriplePatterns.OfType<TriplePattern>().Select(tp => ToSPO(tp, q)).ToList();
                foreach (var row in ExecuteBasicGraphPattern(pats))
                {
                    results.Add(row);
                }
                continue;
            }

            // Get graph specifier as a URI (no variables supported)
            string? graphUri = null;
            IToken? spec = gp.GraphSpecifier;
            if (spec is not null && !string.IsNullOrEmpty(spec.Value))
            {
                var val = spec.Value;
                if (val.StartsWith("<") && val.EndsWith(">"))
                {
                    graphUri = val;
                }
                else if (Uri.IsWellFormedUriString(val, UriKind.Absolute))
                {
                    graphUri = $"<{val}>";
                }
                else if (val.Contains(':'))
                {
                    var idx = val.IndexOf(':');
                    var prefix = val.Substring(0, idx);
                    var local = val.Substring(idx + 1);
                    var ns = q.NamespaceMap.GetNamespaceUri(prefix);
                    if (ns != null)
                    {
                        graphUri = $"<{new Uri(ns, local).AbsoluteUri}>";
                    }
                }
            }

            // If no graph URI (e.g., variable), skip as unsupported
            if (graphUri is null) continue;

            var graphPatterns = gp.TriplePatterns.OfType<TriplePattern>().Select(tp => ToSPO(tp, q)).ToList();
            if (graphPatterns.Count == 0) continue;

            // Execute with a graph restriction by intersecting with graph filter
            IEnumerable<(string subject, string predicate, string obj, string graph)>? current = null;
            foreach (var (s, p, o) in graphPatterns)
            {
                var next = QueryDual(_store, s, p, o, graphUri);
                current = current is null ? next : current.Intersect(next);
            }

            foreach (var (subject, predicate, obj, graph) in current ?? Enumerable.Empty<(string,string,string,string)>())
            {
                var row = new Dictionary<string, string>();
                static string Wrap(string v) => (v.StartsWith("http://") || v.StartsWith("https://")) && !v.StartsWith("<") ? $"<{v}>" : v;
                if (graphPatterns[0].s is null) row["s"] = Wrap(subject);
                if (graphPatterns[0].p is null) row["p"] = Wrap(predicate);
                if (graphPatterns[0].o is null) row["o"] = Wrap(obj);
                results.Add(row);
            }
        }

        return results;
    }

    private static (string s, string p, string o) ToSPO(TriplePattern tp, SparqlQuery q)
    {
        string? s = tp.Subject is NodeMatchPattern smp && smp.Node is VDS.RDF.UriNode su ? su.Uri.AbsoluteUri : null;
        string? p = tp.Predicate is NodeMatchPattern pmp && pmp.Node is VDS.RDF.UriNode pu ? pu.Uri.AbsoluteUri : null;
        string? o = tp.Object is NodeMatchPattern omp && omp.Node is VDS.RDF.UriNode ou ? ou.Uri.AbsoluteUri : null;

        // Try ToString fallback when nulls
        if (s is null)
        {
            var st = tp.Subject?.ToString();
            s = ResolveQName(st, q);
        }
        if (p is null)
        {
            var pt = tp.Predicate?.ToString();
            p = ResolveQName(pt, q);
        }
        if (o is null)
        {
            var ot = tp.Object?.ToString();
            o = ResolveQName(ot, q);
        }

        return (s, p, o);
    }

    private static string ResolveQName(string val, SparqlQuery q)
    {
        if (string.IsNullOrEmpty(val)) return val;
        if (val.StartsWith("<") && val.EndsWith(">")) return val.Trim('<', '>');
        if (Uri.IsWellFormedUriString(val, UriKind.Absolute)) return val;
        // Ignore variable and node debug formats
        if (val.StartsWith("?") || val.StartsWith("[")) return null;
        var idx = val.IndexOf(':');
        if (idx > 0)
        {
            var prefix = val.Substring(0, idx);
            var local = val.Substring(idx + 1);
            var ns = q.NamespaceMap.GetNamespaceUri(prefix);
            if (ns != null)
            {
                return new Uri(ns, local).AbsoluteUri;
            }
        }
        return null;
    }

    private static IEnumerable<(string subject, string predicate, string obj, string graph)> QueryDual(QuadStore store, string? s, string? p, string? o, string? g = null)
    {

        var sVars = Variants(s);
        var pVars = Variants(p);
        var oVars = Variants(o);
        var gVars = Variants(g);

        var seen = new HashSet<(string subject, string predicate, string obj, string graph)>();
        foreach (var sv in sVars)
        foreach (var pv in pVars)
        foreach (var ov in oVars)
        foreach (var gv in gVars)
        {
            foreach (var row in store.Query(subject: sv, predicate: pv, obj: ov, graph: gv))
            {
                if (seen.Add(row))
                    yield return row;
            }
        }
    }

    private static IEnumerable<string> Variants(string v)
    {
        if (v is null) return new string?[] { null };
        // For literals and blank nodes, do not generate angle-bracket variants
        if (v.StartsWith("\"") || v.StartsWith("_:")) return new[] { v };
        if (v.StartsWith("<") && v.EndsWith(">"))
        {
            var raw = v.Trim('<', '>');
            return new[] { v, raw };
        }
        else
        {
            return new[] { v, $"<{v}>" };
        }
    }

    /// <summary>
    /// Executes a very basic BGP-style query: a list of triple patterns (subject, predicate, object).
    /// Any component can be null to indicate a variable.
    /// Returns a list of bindings dictionary mapping variable names to bound values.
    /// </summary>
    public IEnumerable<Dictionary<string, string>> ExecuteBasicGraphPattern(IEnumerable<(string? s, string? p, string? o)> patterns)
    {
        if (patterns is null) throw new ArgumentNullException(nameof(patterns));

        // For now, handle a single pattern by delegating to QuadStore.Query
        var pats = patterns.ToList();
        if (pats.Count == 0) return Enumerable.Empty<Dictionary<string, string>>();

        // Intersect candidate rows across patterns

        IEnumerable<(string subject, string predicate, string obj, string graph)>? current = null;
        foreach (var (s, p, o) in pats)
        {
            var next = QueryDual(_store, s, p, o);
            current = current is null ? next : current.Intersect(next);
        }

        // Materialize bindings
        var results = new List<Dictionary<string, string>>();
        foreach (var (subject, predicate, obj, graph) in current ?? Enumerable.Empty<(string,string,string,string)>())
        {
            var row = new Dictionary<string, string>();
            static string Wrap(string v) => (v.StartsWith("http://") || v.StartsWith("https://")) && !v.StartsWith("<") ? $"<{v}>" : v;
            if (pats[0].s is null) row["s"] = Wrap(subject);
            if (pats[0].p is null) row["p"] = Wrap(predicate);
            if (pats[0].o is null) row["o"] = Wrap(obj);
            // graph variable not supported in minimal version
            results.Add(row);
        }
        return results;
    }
}
