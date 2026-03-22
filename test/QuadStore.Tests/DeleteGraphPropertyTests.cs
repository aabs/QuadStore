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

// Feature: sparql-update-operations, Property 2: Deleted graph excluded from ListGraphs

/// <summary>
/// Property-based test verifying that after deleting a graph,
/// ListGraphNames excludes the deleted graph and retains all others.
/// </summary>
public class DeleteGraphPropertyTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_pbt_delgraph_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Generates a valid absolute URI string: http://example.org/graph/{suffix}
    /// </summary>
    private static Gen<string> GraphUriGen()
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
        return Gen.Choose(1, 8)
            .SelectMany(len =>
                Gen.ArrayOf(Gen.Elements(chars), len)
                    .Select(c => $"http://example.org/graph/{new string(c)}"));
    }

    /// <summary>
    /// Generates a valid absolute URI string for S/P/O components.
    /// </summary>
    private static Gen<string> UriGen()
    {
        var chars = "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray();
        return Gen.Choose(1, 8)
            .SelectMany(len =>
                Gen.ArrayOf(Gen.Elements(chars), len)
                    .Select(c => $"http://example.org/{new string(c)}"));
    }

    /// <summary>
    /// Generates a list of at least 2 distinct graph URIs.
    /// </summary>
    private static Gen<List<string>> DistinctGraphsGen()
    {
        return Gen.Choose(2, 5)
            .SelectMany(count =>
                Gen.ArrayOf(GraphUriGen(), count + 5))
            .Select(arr => arr.Distinct().ToList())
            .Where(list => list.Count >= 2);
    }

    /// <summary>
    /// Generates 1-5 quads for a given graph URI.
    /// </summary>
    private static Gen<List<(string S, string P, string O, string G)>> QuadsForGraphGen(string graphUri)
    {
        return Gen.Choose(1, 5)
            .SelectMany(count =>
                Gen.ArrayOf(
                    UriGen().SelectMany(s =>
                        UriGen().SelectMany(p =>
                            UriGen().Select(o => (s, p, o, graphUri)))),
                    count))
            .Select(arr => arr.Distinct().ToList())
            .Where(list => list.Count > 0);
    }

    /// <summary>
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// For any QuadStore containing quads across multiple named graphs,
    /// and for any graph URI present in the store, after deleting all quads
    /// in that graph via DeleteGraph, ListGraphNames shall not contain that
    /// graph URI, and all other graph URIs shall remain present.
    /// </summary>
    [Fact]
    public void Property2_DeletedGraph_ExcludedFromListGraphNames()
    {
        var property = Prop.ForAll(
            DistinctGraphsGen().ToArbitrary(),
            (List<string> graphs) =>
            {
                // Pick one graph to delete
                return Prop.ForAll(
                    Gen.Elements(graphs.ToArray()).ToArbitrary(),
                    (string graphToDelete) =>
                    {
                        // Generate quads for each graph
                        return Prop.ForAll(
                            GenQuadsForAllGraphs(graphs).ToArbitrary(),
                            (List<(string S, string P, string O, string G)> allQuads) =>
                            {
                                var dir = NewTempDir();
                                try
                                {
                                    var store = new QuadStore(dir);
                                    var provider = new QuadStoreStorageProvider(store);

                                    // Append all quads
                                    foreach (var q in allQuads)
                                    {
                                        store.Append(q.S, q.P, q.O, q.G);
                                    }

                                    // Delete the chosen graph
                                    provider.DeleteGraph(graphToDelete);

                                    // Get remaining graph names
                                    var remainingGraphs = provider.ListGraphNames().ToList();

                                    // Deleted graph must NOT be listed
                                    var deletedExcluded = !remainingGraphs.Contains(graphToDelete);

                                    // All other graphs must still be listed
                                    var otherGraphs = graphs.Where(g => g != graphToDelete).ToList();
                                    var othersPresent = otherGraphs.All(g => remainingGraphs.Contains(g));

                                    return (deletedExcluded && othersPresent)
                                        .Label($"DeletedExcluded={deletedExcluded}, " +
                                               $"OthersPresent={othersPresent}, " +
                                               $"Deleted={graphToDelete}, " +
                                               $"Remaining=[{string.Join(", ", remainingGraphs)}], " +
                                               $"ExpectedOthers=[{string.Join(", ", otherGraphs)}]");
                                }
                                finally
                                {
                                    try { Directory.Delete(dir, true); } catch { }
                                }
                            });
                    });
            });

        property.QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// Generates quads spread across all provided graph URIs,
    /// ensuring each graph has at least one quad.
    /// </summary>
    private static Gen<List<(string S, string P, string O, string G)>> GenQuadsForAllGraphs(List<string> graphs)
    {
        // Generate at least 1 quad per graph, then combine
        var perGraphGens = graphs.Select(g => QuadsForGraphGen(g)).ToArray();

        return perGraphGens.Aggregate(
            Gen.Constant(new List<(string S, string P, string O, string G)>()),
            (acc, gen) => acc.SelectMany(list =>
                gen.Select(quads =>
                {
                    var combined = new List<(string S, string P, string O, string G)>(list);
                    combined.AddRange(quads);
                    return combined;
                })));
    }
}
