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

// Feature: sparql-update-operations, Property 5: DELETE DATA removes specified quads

/// <summary>
/// Property-based tests verifying that executing a SPARQL DELETE DATA command
/// via the StorageProvider removes the specified quads while leaving others intact.
/// </summary>
public class DeleteDataPropertyTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_pbt_del_" + Guid.NewGuid());
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
    /// Generates two non-overlapping lists: allQuads (2..15) and a non-empty subset toDelete.
    /// Returns (allQuads, toDelete) where toDelete is a subset of allQuads.
    /// </summary>
    private static Gen<(List<(string S, string P, string O, string G)> All,
                         List<(string S, string P, string O, string G)> ToDelete)> QuadSplitGen()
    {
        return Gen.Choose(2, 15)
            .SelectMany(count =>
                Gen.ArrayOf(QuadGen(), count))
            .Select(arr => arr.Distinct().ToList())
            .Where(list => list.Count >= 2)
            .SelectMany(allQuads =>
            {
                // Pick a random non-empty subset to delete (at least 1, at most allQuads.Count - 1)
                var maxDelete = Math.Max(1, allQuads.Count - 1);
                return Gen.Choose(1, maxDelete)
                    .SelectMany(deleteCount =>
                        Gen.Shuffle(allQuads.ToArray())
                            .Select(shuffled => shuffled.Take(deleteCount).ToList()))
                    .Select(toDelete => (All: allQuads, ToDelete: toDelete));
            });
    }

    /// <summary>
    /// Builds a SPARQL DELETE DATA string with GRAPH clauses for the given quads.
    /// </summary>
    private static string BuildDeleteDataSparql(
        List<(string S, string P, string O, string G)> quads)
    {
        var grouped = quads.GroupBy(q => q.G);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("DELETE DATA {");
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
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    ///
    /// For any set of quads present in the store, executing a SPARQL
    /// DELETE DATA { GRAPH &lt;uri&gt; { triples } } command via Update shall result
    /// in those quads no longer being returned by Query, while all other quads
    /// remain unaffected.
    /// </summary>
    [Fact]
    public void Property5_DeleteData_RemovesSpecifiedQuads()
    {
        var property = Prop.ForAll(
            QuadSplitGen().ToArbitrary(),
            ((List<(string S, string P, string O, string G)> All,
              List<(string S, string P, string O, string G)> ToDelete) input) =>
            {
                var dir = NewTempDir();
                try
                {
                    var store = new QuadStore(dir);
                    var provider = new QuadStoreStorageProvider(store);

                    // Append all quads directly to the store
                    foreach (var q in input.All)
                    {
                        store.Append(q.S, q.P, q.O, q.G);
                    }

                    // Build and execute SPARQL DELETE DATA for the subset
                    var sparql = BuildDeleteDataSparql(input.ToDelete);
                    provider.Update(sparql);

                    // Query all remaining quads
                    var remaining = store.Query()
                        .Select(q => (q.subject, q.predicate, q.obj, q.graph))
                        .ToHashSet();

                    var toDeleteSet = input.ToDelete.ToHashSet();
                    var expectedRemaining = input.All
                        .Where(q => !toDeleteSet.Contains(q))
                        .Select(q => (q.S, q.P, q.O, q.G))
                        .ToHashSet();

                    // Deleted quads must be gone
                    var stillPresent = input.ToDelete
                        .Where(q => remaining.Contains((q.S, q.P, q.O, q.G)))
                        .ToList();

                    // Non-deleted quads must still be present
                    var missing = expectedRemaining
                        .Where(q => !remaining.Contains(q))
                        .ToList();

                    return (stillPresent.Count == 0)
                        .Label($"Deleted quads still present: {stillPresent.Count}")
                        .And((missing.Count == 0)
                            .Label($"Non-deleted quads missing: {missing.Count}"));
                }
                finally
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            });

        property.QuickCheckThrowOnFailure();
    }
}
