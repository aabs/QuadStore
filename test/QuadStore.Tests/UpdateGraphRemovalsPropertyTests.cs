using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FsCheck;
using FsCheck.Fluent;
using FluentAssertions;
using TripleStore.Core;
using VDS.RDF;
using Xunit;

namespace TripleStore.Tests;

// Feature: sparql-update-operations, Property 3: UpdateGraph removals delete matching quads

/// <summary>
/// Property-based test verifying that calling UpdateGraph with a removals
/// collection causes those triples to no longer appear in LoadGraph, while
/// triples not in the removals collection remain.
/// </summary>
public class UpdateGraphRemovalsPropertyTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_pbt_updrem_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
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
    /// Generates a triple as a tuple of three URI strings (S, P, O).
    /// </summary>
    private static Gen<(string S, string P, string O)> TripleGen()
    {
        return UriGen().SelectMany(s =>
            UriGen().SelectMany(p =>
                UriGen().Select(o => (s, p, o))));
    }

    /// <summary>
    /// Generates a non-empty list of distinct triples (2..15).
    /// </summary>
    private static Gen<List<(string S, string P, string O)>> TripleListGen()
    {
        return Gen.Choose(2, 15)
            .SelectMany(count =>
                Gen.ArrayOf(TripleGen(), count + 5))
            .Select(arr => arr.Distinct().ToList())
            .Where(list => list.Count >= 2);
    }

    /// <summary>
    /// Creates a dotNetRDF Triple from URI strings using the given NodeFactory.
    /// </summary>
    private static Triple MakeTriple(NodeFactory factory, string s, string p, string o)
    {
        return new Triple(
            factory.CreateUriNode(new Uri(s)),
            factory.CreateUriNode(new Uri(p)),
            factory.CreateUriNode(new Uri(o)));
    }

    /// <summary>
    /// Extracts a set of (S, P, O) tuples from a loaded dotNetRDF Graph,
    /// using the AbsoluteUri of each URI node.
    /// </summary>
    private static HashSet<(string S, string P, string O)> ExtractTriples(IGraph g)
    {
        var set = new HashSet<(string, string, string)>();
        foreach (var t in g.Triples)
        {
            var s = ((IUriNode)t.Subject).Uri.AbsoluteUri;
            var p = ((IUriNode)t.Predicate).Uri.AbsoluteUri;
            var o = ((IUriNode)t.Object).Uri.AbsoluteUri;
            set.Add((s, p, o));
        }
        return set;
    }

    /// <summary>
    /// Generates a non-empty proper subset of the given triples to use as removals.
    /// Each triple has a 50% chance of being selected, with at least 1 and fewer than all selected.
    /// </summary>
    private static Gen<List<(string S, string P, string O)>> RemovalSubsetGen(
        List<(string S, string P, string O)> allTriples)
    {
        return Gen.ArrayOf(Gen.Elements(true, false), allTriples.Count)
            .Select((bool[] flags) =>
            {
                var subset = new List<(string S, string P, string O)>();
                for (int i = 0; i < flags.Length; i++)
                {
                    if (flags[i])
                        subset.Add(allTriples[i]);
                }
                return subset;
            })
            .Where((List<(string S, string P, string O)> subset) =>
                subset.Count > 0 && subset.Count < allTriples.Count);
    }

    /// <summary>
    /// **Validates: Requirements 4.1**
    ///
    /// For any set of triples previously added to a graph via the StorageProvider,
    /// calling UpdateGraph with a subset of those triples in the removals collection
    /// shall result in those triples no longer being returned by LoadGraph for that
    /// graph, while all other triples remain present.
    /// </summary>
    [Fact]
    public void Property3_UpdateGraphRemovals_DeleteMatchingQuads()
    {
        var property = Prop.ForAll(
            TripleListGen().ToArbitrary(),
            (List<(string S, string P, string O)> allTriples) =>
            {
                return Prop.ForAll(
                    UriGen().Select(u => $"http://example.org/graph/{u}").ToArbitrary(),
                    (string graphUri) =>
                    {
                        return Prop.ForAll(
                            RemovalSubsetGen(allTriples).ToArbitrary(),
                            (List<(string S, string P, string O)> removals) =>
                            {
                                var dir = NewTempDir();
                                try
                                {
                                    var store = new QuadStore(dir);
                                    var provider = new QuadStoreStorageProvider(store);
                                    var factory = new NodeFactory();
                                    var graphUriObj = new Uri(graphUri);

                                    // Add all triples via SaveGraph
                                    var g = new Graph();
                                    g.BaseUri = graphUriObj;
                                    foreach (var t in allTriples)
                                    {
                                        g.Assert(MakeTriple(factory, t.S, t.P, t.O));
                                    }
                                    provider.SaveGraph(g);

                                    // Remove subset via UpdateGraph
                                    var removalTriples = removals
                                        .Select(t => MakeTriple(factory, t.S, t.P, t.O))
                                        .ToList();
                                    provider.UpdateGraph(graphUriObj, null!, removalTriples);

                                    // Load graph and check
                                    var loaded = new Graph();
                                    provider.LoadGraph(loaded, graphUriObj);
                                    var remaining = ExtractTriples(loaded);

                                    var removalSet = removals.ToHashSet();
                                    var expectedSurvivors = allTriples
                                        .Where(t => !removalSet.Contains(t))
                                        .ToHashSet();

                                    // Removed triples must not be present
                                    var removedStillPresent = removals
                                        .Where(t => remaining.Contains(t))
                                        .ToList();

                                    // Survivors must all be present
                                    var survivorsMissing = expectedSurvivors
                                        .Where(t => !remaining.Contains(t))
                                        .ToList();

                                    return (removedStillPresent.Count == 0 && survivorsMissing.Count == 0)
                                        .Label($"RemovedStillPresent={removedStillPresent.Count}, " +
                                               $"SurvivorsMissing={survivorsMissing.Count}, " +
                                               $"Total={allTriples.Count}, " +
                                               $"Removed={removals.Count}, " +
                                               $"Remaining={remaining.Count}");
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
}
