using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FsCheck;
using FsCheck.Fluent;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

// Feature: sparql-update-operations, Property 4: INSERT DATA round-trip

/// <summary>
/// Property-based tests verifying that executing a SPARQL INSERT DATA command
/// via the StorageProvider results in all specified triples being queryable.
/// </summary>
public class InsertDataPropertyTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_pbt_ins_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Generates a valid absolute URI string: http://example.org/{suffix}
    /// </summary>
    private static Gen<string> UriGen()
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
        return Gen.Choose(1, 10)
            .SelectMany(len =>
                Gen.ArrayOf(Gen.Elements(chars), len)
                    .Select(c => $"http://example.org/{new string(c)}"));
    }

    /// <summary>
    /// Generates a quad as a tuple of four URI strings (S, P, O, G).
    /// </summary>
    private static Gen<(string S, string P, string O, string G)> QuadGen()
    {
        return UriGen().SelectMany(s =>
            UriGen().SelectMany(p =>
                UriGen().SelectMany(o =>
                    UriGen().Select(g => (s, p, o, g)))));
    }

    /// <summary>
    /// Generates a non-empty list of distinct quads (1..10).
    /// </summary>
    private static Gen<List<(string S, string P, string O, string G)>> QuadListGen()
    {
        return Gen.Choose(1, 10)
            .SelectMany(count =>
                Gen.ArrayOf(QuadGen(), count))
            .Select(arr => arr.Distinct().ToList())
            .Where(list => list.Count > 0);
    }

    /// <summary>
    /// Builds a SPARQL INSERT DATA string with a GRAPH clause containing the given triples.
    /// </summary>
    private static string BuildInsertDataSparql(
        List<(string S, string P, string O, string G)> quads)
    {
        var grouped = quads.GroupBy(q => q.G);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("INSERT DATA {");
        foreach (var group in grouped)
        {
            sb.AppendLine($"  GRAPH <{group.Key}> {{");
            foreach (var q in group)
            {
                sb.AppendLine($"    <{q.S}> <{q.P}> <{q.O}> .");
            }
            sb.AppendLine("  }");
        }
        sb.AppendLine("}");
        return sb.ToString();
    }

    /// <summary>
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    ///
    /// For any set of valid RDF triples and any graph URI, executing a SPARQL
    /// INSERT DATA { GRAPH &lt;uri&gt; { triples } } command via Update shall result
    /// in all specified triples being queryable in the specified graph.
    /// </summary>
    [Fact]
    public void Property4_InsertData_RoundTrip()
    {
        var property = Prop.ForAll(
            QuadListGen().ToArbitrary(),
            (List<(string S, string P, string O, string G)> quads) =>
            {
                var dir = NewTempDir();
                try
                {
                    var store = new QuadStore(dir);
                    var provider = new QuadStoreStorageProvider(store);

                    // Build and execute SPARQL INSERT DATA
                    var sparql = BuildInsertDataSparql(quads);
                    provider.Update(sparql);

                    // Query all quads from the store
                    var allQuads = store.Query().ToList();
                    var allQuadSet = allQuads
                        .Select(q => (q.subject, q.predicate, q.obj, q.graph))
                        .ToHashSet();

                    // Every inserted quad must be queryable
                    var missing = quads
                        .Where(q => !allQuadSet.Contains((q.S, q.P, q.O, q.G)))
                        .ToList();

                    return (missing.Count == 0)
                        .Label($"Missing quads after INSERT DATA: {missing.Count} of {quads.Count}");
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            });

        property.QuickCheckThrowOnFailure();
    }
}
