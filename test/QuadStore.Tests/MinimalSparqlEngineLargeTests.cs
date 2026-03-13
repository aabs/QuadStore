using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using SparqlEngine;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class MinimalSparqlEngineLargeTests
{
    private static QuadStore NewStore()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sparql_large_" + Guid.NewGuid());
        System.IO.Directory.CreateDirectory(dir);
        return new QuadStore(dir);
    }

    [Fact]
    public void LargeDataset_SimpleQuery_ReturnsExpectedCount()
    {
        var store = NewStore();
        // Insert 50k knows edges and 50k type edges
        int n = 50_000;
        for (int i = 0; i < n; i++)
        {
            store.Append($"<http://example.org/S{i}>", "<http://example.org/knows>", $"<http://example.org/O{i}>", "<http://example.org/G>");
            store.Append($"<http://example.org/S{i}>", "<http://example.org/type>", "<http://example.org/Person>", "<http://example.org/G>");
        }
        var engine = new MinimalSparqlEngine(store);
        var sparql = "PREFIX ex: <http://example.org/>\nSELECT ?s ?o WHERE { ?s ex:knows ?o }";
        var res = engine.ExecuteQuery(sparql).ToList();
        res.Should().HaveCount(n);
    }

    [Fact]
    public void NamedGraphs_QueryIgnoresGraphClause_CurrentEngine()
    {
        var store = NewStore();
        // Same triple in two different graphs
        store.Append("<http://example.org/Ada>", "<http://example.org/knows>", "<http://example.org/Bob>", "<http://example.org/G1>");
        store.Append("<http://example.org/Ada>", "<http://example.org/knows>", "<http://example.org/Bob>", "<http://example.org/G2>");
        var engine = new MinimalSparqlEngine(store);
        var sparql = "PREFIX ex: <http://example.org/>\nSELECT ?s ?o WHERE { ?s ex:knows ?o }";
        var res = engine.ExecuteQuery(sparql).ToList();
        // Engine matches triples across graphs, returning two bindings
        res.Should().HaveCount(2);
        res.All(r => r["s"] == "<http://example.org/Ada>" && r["o"] == "<http://example.org/Bob>").Should().BeTrue();
    }

    [Fact]
    public void NamedGraphs_DirectQuadStoreGraphFilter_Works()
    {
        var store = NewStore();
        store.Append("<http://example.org/Ada>", "<http://example.org/knows>", "<http://example.org/Bob>", "<http://example.org/G1>");
        store.Append("<http://example.org/Ada>", "<http://example.org/knows>", "<http://example.org/Bob>", "<http://example.org/G2>");
        store.Append("<http://example.org/Ada>", "<http://example.org/type>", "<http://example.org/Person>", "<http://example.org/G1>");
        var g1 = store.Query(graph: "<http://example.org/G1>").ToList();
        var g2 = store.Query(graph: "<http://example.org/G2>").ToList();
        g1.Any(t => t.subject == "<http://example.org/Ada>" && t.predicate == "<http://example.org/knows>" && t.obj == "<http://example.org/Bob>").Should().BeTrue();
        g1.Any(t => t.predicate == "<http://example.org/type>").Should().BeTrue();
        g2.Single().Should().Be(("<http://example.org/Ada>", "<http://example.org/knows>", "<http://example.org/Bob>", "<http://example.org/G2>"));
    }

    [Fact]
    public void LargeDataset_MultiThreadedInsertsAndQuery()
    {
        var store = NewStore();
        int workers = 8, perWorker = 10_000;
        Parallel.For(0, workers, i =>
        {
            for (int j = 0; j < perWorker; j++)
            {
                store.Append($"<http://example.org/S{i}-{j}>", "<http://example.org/knows>", $"<http://example.org/O{i}-{j}>", "<http://example.org/G>");
            }
        });
        var engine = new MinimalSparqlEngine(store);
        var sparql = "PREFIX ex: <http://example.org/>\nSELECT ?s ?o WHERE { ?s ex:knows ?o }";
        engine.ExecuteQuery(sparql).Count().Should().Be(workers * perWorker);
    }
}
