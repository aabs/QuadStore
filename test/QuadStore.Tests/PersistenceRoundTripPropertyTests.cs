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

// Feature: sparql-update-operations, Property 6: SaveAll/LoadAll round-trip preserves deletion state

/// <summary>
/// Property-based test verifying that SaveAll/LoadAll round-trip preserves deletion state.
/// After a sequence of appends and deletes, saving and reloading the store produces
/// identical Query results.
/// </summary>
public class PersistenceRoundTripPropertyTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_pbt_persist_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Generates a valid URI-like string: http://example.org/{suffix}
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
    /// Generates a quad as a tuple of four URI strings.
    /// </summary>
    private static Gen<(string S, string P, string O, string G)> QuadGen()
    {
        return UriGen().SelectMany(s =>
            UriGen().SelectMany(p =>
                UriGen().SelectMany(o =>
                    UriGen().Select(g => (s, p, o, g)))));
    }

    /// <summary>
    /// Generates a non-empty list of distinct quads (1..20).
    /// </summary>
    private static Gen<List<(string S, string P, string O, string G)>> QuadListGen()
    {
        return Gen.Choose(1, 20)
            .SelectMany(count =>
                Gen.ArrayOf(QuadGen(), count))
            .Select(arr => arr.Distinct().ToList())
            .Where(list => list.Count > 0);
    }

    /// <summary>
    /// Represents a delete pattern where each component is either a specific value or null (wildcard).
    /// </summary>
    private record DeletePattern(string? Subject, string? Predicate, string? Obj, string? Graph);

    /// <summary>
    /// Generates a delete pattern by picking values from the existing quads.
    /// Each component has a 50% chance of being null (wildcard) or a value
    /// drawn from the corresponding component of the quads. At least one
    /// component is guaranteed to be non-null.
    /// </summary>
    private static Gen<DeletePattern> DeletePatternGen(
        List<(string S, string P, string O, string G)> quads)
    {
        var subjects = quads.Select(q => q.S).Distinct().ToArray();
        var predicates = quads.Select(q => q.P).Distinct().ToArray();
        var objects = quads.Select(q => q.O).Distinct().ToArray();
        var graphs = quads.Select(q => q.G).Distinct().ToArray();

        Gen<string?> MaybePickFrom(string[] values) =>
            Gen.OneOf(
                Gen.Constant<string?>(null),
                Gen.Elements(values).Select<string, string?>(v => v));

        return MaybePickFrom(subjects).SelectMany(s =>
            MaybePickFrom(predicates).SelectMany(p =>
                MaybePickFrom(objects).SelectMany(o =>
                    MaybePickFrom(graphs).Select(g => new DeletePattern(s, p, o, g)))))
            .Where(dp => dp.Subject is not null
                      || dp.Predicate is not null
                      || dp.Obj is not null
                      || dp.Graph is not null);
    }

    /// <summary>
    /// **Validates: Requirements 9.1, 9.2, 9.3**
    ///
    /// For any QuadStore that has had a sequence of appends and deletes applied,
    /// calling SaveAll and then creating a new QuadStore from the same directory
    /// (which calls LoadAll) shall produce a store where Query with no filters
    /// returns the exact same set of quads as the original store.
    /// </summary>
    [Fact]
    public void Property6_SaveAllLoadAll_RoundTrip_PreservesDeletionState()
    {
        var property = Prop.ForAll(
            QuadListGen().ToArbitrary(),
            (List<(string S, string P, string O, string G)> quads) =>
            {
                return Prop.ForAll(
                    DeletePatternGen(quads).ToArbitrary(),
                    (DeletePattern pattern) =>
                    {
                        var dir = NewTempDir();
                        try
                        {
                            List<(string subject, string predicate, string obj, string graph)> originalResults;

                            // Create store, append quads, delete by pattern, save, then dispose
                            using (var originalStore = new QuadStore(dir))
                            {
                                foreach (var q in quads)
                                {
                                    originalStore.Append(q.S, q.P, q.O, q.G);
                                }

                                originalStore.Delete(pattern.Subject, pattern.Predicate,
                                                     pattern.Obj, pattern.Graph);

                                // Capture query results before save
                                originalResults = originalStore.Query()
                                    .OrderBy(q => q.subject)
                                    .ThenBy(q => q.predicate)
                                    .ThenBy(q => q.obj)
                                    .ThenBy(q => q.graph)
                                    .ToList();

                                // Persist to disk
                                originalStore.SaveAll();
                            }

                            // Reload from same directory — constructor calls LoadAll
                            List<(string subject, string predicate, string obj, string graph)> reloadedResults;
                            using (var reloadedStore = new QuadStore(dir))
                            {
                                reloadedResults = reloadedStore.Query()
                                    .OrderBy(q => q.subject)
                                    .ThenBy(q => q.predicate)
                                    .ThenBy(q => q.obj)
                                    .ThenBy(q => q.graph)
                                    .ToList();
                            }

                            var countsMatch = originalResults.Count == reloadedResults.Count;
                            var contentsMatch = originalResults.SequenceEqual(reloadedResults);

                            return (countsMatch && contentsMatch)
                                .Label($"Original count: {originalResults.Count}, " +
                                       $"Reloaded count: {reloadedResults.Count}, " +
                                       $"Pattern: S={pattern.Subject ?? "*"} P={pattern.Predicate ?? "*"} " +
                                       $"O={pattern.Obj ?? "*"} G={pattern.Graph ?? "*"}");
                        }
                        finally
                        {
                            try { Directory.Delete(dir, true); } catch { }
                        }
                    });
            });

        property.QuickCheckThrowOnFailure();
    }
}
