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
}
