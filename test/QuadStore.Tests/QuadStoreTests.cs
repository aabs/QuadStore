using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class QuadStoreTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_store_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Test_AppendAndQuery_SingleQuad()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:Graph1");
        var rows = qs.Query(subject: "ex:Ada").ToList();
        rows.Should().ContainSingle();
        rows[0].Should().Be(("ex:Ada", "ex:type", "ex:Person", "ex:Graph1"));
    }

    [Fact]
    public void Test_AppendMultipleQuads_QueryByPredicate()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:Graph1");
        qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:Graph2");
        qs.Append("ex:Ada", "ex:knows", "ex:Bob", "ex:Graph1");
        var rows = qs.Query(predicate: "ex:type").ToList();
        rows.Should().HaveCount(2);
        rows.Should().Contain(("ex:Ada", "ex:type", "ex:Person", "ex:Graph1"));
        rows.Should().Contain(("ex:Bob", "ex:type", "ex:Person", "ex:Graph2"));
    }

    [Fact]
    public void Test_Query_SubjectAndPredicateIntersection()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:Graph1");
        qs.Append("ex:Ada", "ex:knows", "ex:Bob", "ex:Graph1");
        var rows = qs.Query(subject: "ex:Ada", predicate: "ex:knows").ToList();
        rows.Should().ContainSingle();
        rows[0].Should().Be(("ex:Ada", "ex:knows", "ex:Bob", "ex:Graph1"));
    }

    [Fact]
    public void Test_SaveAndLoadAll_StoreIntegrity()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:Graph1");
        qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:Graph2");
        qs.SaveAll();

        var qs2 = new QuadStore(dir);
        var rows = qs2.Query().ToList();
        rows.Should().HaveCount(2);
        rows.Should().Contain(("ex:Ada", "ex:type", "ex:Person", "ex:Graph1"));
        rows.Should().Contain(("ex:Bob", "ex:type", "ex:Person", "ex:Graph2"));
    }

    [Fact]
    public async Task Test_ConcurrentAppends_NoDataLoss()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        int workers = 16, perWorker = 1000;
        var tasks = Enumerable.Range(0, workers).Select(i => Task.Run(() =>
        {
            for (int j = 0; j < perWorker; j++)
            {
                qs.Append($"ex:S{i}-{j}", "ex:type", "ex:Person", "ex:G");
            }
        }));
        await Task.WhenAll(tasks);
        var count = qs.Query(predicate: "ex:type").Count();
        count.Should().Be(workers * perWorker);
    }

    [Fact]
    public async Task Test_ConcurrentQueries_CorrectResults()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G");
        qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G");
        qs.Append("ex:Ada", "ex:knows", "ex:Bob", "ex:G");

        var tasks = new[]
        {
            Task.Run(() => qs.Query(subject: "ex:Ada").ToList()),
            Task.Run(() => qs.Query(predicate: "ex:type").ToList()),
            Task.Run(() => qs.Query(obj: "ex:Bob").ToList()),
        };
        var results = await Task.WhenAll(tasks);
        results[0].Should().HaveCount(2);
        results[1].Should().HaveCount(2);
        results[2].Should().HaveCount(1);
    }

    [Fact]
    public void Test_QueryOnEmptyStore_ReturnsEmpty()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Query(subject: "ex:Nope").Should().BeEmpty();
    }

    // ── Delete tests (Requirements 1.1, 1.2, 1.4, 1.6) ──

    [Fact]
    public void Test_Delete_SingleQuadByAllFourComponents()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G1");

        var deleted = qs.Delete(subject: "ex:Ada", predicate: "ex:type", obj: "ex:Person", graph: "ex:G1");

        deleted.Should().Be(1);
        var remaining = qs.Query().ToList();
        remaining.Should().ContainSingle();
        remaining[0].Should().Be(("ex:Bob", "ex:type", "ex:Person", "ex:G1"));
    }

    [Fact]
    public void Test_Delete_BySubjectOnly()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Ada", "ex:knows", "ex:Bob", "ex:G1");
        qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G1");

        var deleted = qs.Delete(subject: "ex:Ada");

        deleted.Should().Be(2);
        var remaining = qs.Query().ToList();
        remaining.Should().ContainSingle();
        remaining[0].Should().Be(("ex:Bob", "ex:type", "ex:Person", "ex:G1"));
    }

    [Fact]
    public void Test_Delete_ByPredicateOnly()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Ada", "ex:knows", "ex:Bob", "ex:G1");

        var deleted = qs.Delete(predicate: "ex:type");

        deleted.Should().Be(2);
        var remaining = qs.Query().ToList();
        remaining.Should().ContainSingle();
        remaining[0].Should().Be(("ex:Ada", "ex:knows", "ex:Bob", "ex:G1"));
    }

    [Fact]
    public void Test_Delete_ByObjectOnly()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Bob", "ex:type", "ex:Animal", "ex:G1");

        var deleted = qs.Delete(obj: "ex:Person");

        deleted.Should().Be(1);
        var remaining = qs.Query().ToList();
        remaining.Should().ContainSingle();
        remaining[0].Should().Be(("ex:Bob", "ex:type", "ex:Animal", "ex:G1"));
    }

    [Fact]
    public void Test_Delete_ByGraphOnly()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G2");

        var deleted = qs.Delete(graph: "ex:G1");

        deleted.Should().Be(1);
        var remaining = qs.Query().ToList();
        remaining.Should().ContainSingle();
        remaining[0].Should().Be(("ex:Bob", "ex:type", "ex:Person", "ex:G2"));
    }

    [Fact]
    public void Test_Delete_AllWithNullFilters()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G2");
        qs.Append("ex:Ada", "ex:knows", "ex:Bob", "ex:G1");

        var deleted = qs.Delete();

        deleted.Should().Be(3);
        qs.Query().Should().BeEmpty();
    }

    [Fact]
    public void Test_Delete_NonExistentPatternReturnsZero()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");

        var deleted = qs.Delete(subject: "ex:Nobody");

        deleted.Should().Be(0);
        qs.Query().Should().ContainSingle();
    }

    [Fact]
    public void Test_Delete_MultipleMatchingRows()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Carol", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Ada", "ex:knows", "ex:Bob", "ex:G1");

        var deleted = qs.Delete(predicate: "ex:type", graph: "ex:G1");

        deleted.Should().Be(3);
        var remaining = qs.Query().ToList();
        remaining.Should().ContainSingle();
        remaining[0].Should().Be(("ex:Ada", "ex:knows", "ex:Bob", "ex:G1"));
    }

    [Fact]
    public void Test_Delete_QueryExcludesDeletedQuads()
    {
        var dir = NewTempDir();
        var qs = new QuadStore(dir);
        qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
        qs.Append("ex:Ada", "ex:knows", "ex:Bob", "ex:G1");

        qs.Delete(predicate: "ex:type");

        // Unfiltered query should not return the deleted quad
        var all = qs.Query().ToList();
        all.Should().ContainSingle();
        all[0].Should().Be(("ex:Ada", "ex:knows", "ex:Bob", "ex:G1"));

        // Filtered query targeting the deleted quad should return nothing
        qs.Query(predicate: "ex:type").Should().BeEmpty();

        // Filtered query targeting the surviving quad should still work
        qs.Query(predicate: "ex:knows").Should().ContainSingle();
    }

    // ── Tombstone persistence tests (Requirements 9.1, 9.2, 9.3) ──

    [Fact]
    public void Test_SaveAll_WritesTombstonesBinFile()
    {
        var dir = NewTempDir();
        using (var qs = new QuadStore(dir))
        {
            qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
            qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G1");
            qs.Delete(subject: "ex:Ada");
            qs.SaveAll();
        }

        var tombstonePath = Path.Combine(dir, "tombstones.bin");
        File.Exists(tombstonePath).Should().BeTrue("SaveAll should persist tombstones.bin");

        // Reload and verify tombstones were restored
        using (var qs2 = new QuadStore(dir))
        {
            var rows = qs2.Query().ToList();
            rows.Should().ContainSingle();
            rows[0].Should().Be(("ex:Bob", "ex:type", "ex:Person", "ex:G1"));
        }
    }

    [Fact]
    public void Test_TombstonePersistence_RoundTrip()
    {
        var dir = NewTempDir();

        // Append, delete, save
        using (var qs = new QuadStore(dir))
        {
            qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
            qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G1");
            qs.Append("ex:Carol", "ex:knows", "ex:Ada", "ex:G2");
            qs.Delete(subject: "ex:Bob");
            qs.SaveAll();
        }

        // Reload and verify correct results
        using (var qs2 = new QuadStore(dir))
        {
            var all = qs2.Query().ToList();
            all.Should().HaveCount(2);
            all.Should().Contain(("ex:Ada", "ex:type", "ex:Person", "ex:G1"));
            all.Should().Contain(("ex:Carol", "ex:knows", "ex:Ada", "ex:G2"));

            // Deleted quad should not appear in filtered queries either
            qs2.Query(subject: "ex:Bob").Should().BeEmpty();
        }
    }

    [Fact]
    public void Test_LoadStore_WithNoTombstonesBinFile()
    {
        var dir = NewTempDir();

        // Append data and save — no deletes, so tombstones.bin should be empty or minimal
        using (var qs = new QuadStore(dir))
        {
            qs.Append("ex:Ada", "ex:type", "ex:Person", "ex:G1");
            qs.Append("ex:Bob", "ex:type", "ex:Person", "ex:G1");
            qs.SaveAll();
        }

        // Delete tombstones.bin to simulate a clean start / legacy store
        var tombstonePath = Path.Combine(dir, "tombstones.bin");
        if (File.Exists(tombstonePath))
        {
            File.Delete(tombstonePath);
        }

        File.Exists(tombstonePath).Should().BeFalse("tombstones.bin should not exist for this test");

        // Reload — should work fine with no tombstones.bin
        using (var qs2 = new QuadStore(dir))
        {
            var rows = qs2.Query().ToList();
            rows.Should().HaveCount(2);
            rows.Should().Contain(("ex:Ada", "ex:type", "ex:Person", "ex:G1"));
            rows.Should().Contain(("ex:Bob", "ex:type", "ex:Person", "ex:G1"));
        }
    }
}
