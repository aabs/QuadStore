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

// Feature: sparql-update-operations, Property 1: Delete-then-query exclusion

/// <summary>
/// Property-based tests verifying that after a delete operation,
/// Query returns no deleted quads and all non-deleted quads remain.
/// </summary>
public class DeleteThenQueryPropertyTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_pbt_del_" + Guid.NewGuid());
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
    /// Determines whether a quad matches a delete pattern.
    /// Null components in the pattern act as wildcards (match anything).
    /// </summary>
    private static bool Matches(
        (string S, string P, string O, string G) quad,
        DeletePattern pattern)
    {
        if (pattern.Subject is not null && quad.S != pattern.Subject) return false;
        if (pattern.Predicate is not null && quad.P != pattern.Predicate) return false;
        if (pattern.Obj is not null && quad.O != pattern.Obj) return false;
        if (pattern.Graph is not null && quad.G != pattern.Graph) return false;
        return true;
    }

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
    /// **Validates: Requirements 1.1, 1.2, 2.2**
    ///
    /// For any set of quads and any delete pattern derived from existing values,
    /// after appending all quads and deleting by the pattern:
    /// - Query returns no quads that matched the delete pattern
    /// - Query returns all quads that did NOT match the delete pattern
    /// </summary>
    [Fact]
    public void Property1_DeleteThenQuery_ExcludesDeletedAndRetainsRest()
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
                            var store = new QuadStore(dir);

                            // Append all quads
                            foreach (var q in quads)
                            {
                                store.Append(q.S, q.P, q.O, q.G);
                            }

                            // Compute expected sets before delete
                            var expectedDeleted = quads
                                .Where(q => Matches(q, pattern))
                                .ToHashSet();
                            var expectedSurvivors = quads
                                .Where(q => !Matches(q, pattern))
                                .ToHashSet();

                            // Perform delete
                            store.Delete(pattern.Subject, pattern.Predicate,
                                         pattern.Obj, pattern.Graph);

                            // Query all remaining quads
                            var remaining = store.Query().ToList();
                            var remainingSet = remaining.ToHashSet();

                            // No deleted quad should be present
                            var deletedStillPresent = expectedDeleted
                                .Where(d => remainingSet.Contains(
                                    (d.S, d.P, d.O, d.G)))
                                .ToList();

                            // All survivors should still be present
                            var survivorsMissing = expectedSurvivors
                                .Where(s => !remainingSet.Contains(
                                    (s.S, s.P, s.O, s.G)))
                                .ToList();

                            var noDeletedPresent = deletedStillPresent.Count == 0;
                            var allSurvivorsPresent = survivorsMissing.Count == 0;

                            return (noDeletedPresent && allSurvivorsPresent)
                                .Label($"Deleted still present: {deletedStillPresent.Count}, " +
                                       $"Survivors missing: {survivorsMissing.Count}, " +
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
