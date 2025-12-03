using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using TripleStore.Core;
using SparqlEngine;

namespace TripleStore.Benchmarks;

/// <summary>
/// Benchmarks for SPARQL query execution including parsing, translation, and execution.
/// Tests simple patterns, joins, and graph filtering.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5)]
public class SparqlEngineBenchmarks
{
    private string _tempDir = null!;
    private QuadStore _store = null!;
    private MinimalSparqlEngine _engine = null!;

    [Params(10_000, 100_000)]
    public int DatasetSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"benchmark_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new QuadStore(_tempDir);

        // Create a dataset with social network structure
        int peopleCount = (int)Math.Sqrt(DatasetSize);
        int connectionsPerPerson = DatasetSize / peopleCount;

        for (int i = 0; i < peopleCount; i++)
        {
            // Person attributes
            _store.Append(
                $"http://example.org/person{i}",
                "http://example.org/name",
                $"\"Person {i}\"",
                "http://example.org/defaultGraph"
            );
            
            _store.Append(
                $"http://example.org/person{i}",
                "http://example.org/age",
                $"\"{20 + (i % 60)}\"",
                "http://example.org/defaultGraph"
            );

            // Connections
            for (int j = 0; j < connectionsPerPerson && (i * connectionsPerPerson + j) < DatasetSize; j++)
            {
                int friendId = (i + j + 1) % peopleCount;
                _store.Append(
                    $"http://example.org/person{i}",
                    "http://example.org/knows",
                    $"http://example.org/person{friendId}",
                    "http://example.org/socialGraph"
                );
            }
        }

        _engine = new MinimalSparqlEngine(_store);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _store.Dispose();
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, true);
            }
        }
        catch { /* Ignore cleanup errors */ }
    }

    [Benchmark(Baseline = true)]
    public int SimpleSparqlQuery()
    {
        var query = @"
            PREFIX ex: <http://example.org/>
            SELECT ?name WHERE {
                ex:person100 ex:name ?name .
            }
        ";
        var results = _engine.ExecuteQuery(query).ToList();
        return results.Count;
    }

    [Benchmark]
    public int SparqlQueryWithMultiplePatterns()
    {
        var query = @"
            PREFIX ex: <http://example.org/>
            SELECT ?name ?age WHERE {
                ex:person100 ex:name ?name .
                ex:person100 ex:age ?age .
            }
        ";
        var results = _engine.ExecuteQuery(query).ToList();
        return results.Count;
    }

    [Benchmark]
    public int SparqlQueryAllVariables()
    {
        var query = @"
            PREFIX ex: <http://example.org/>
            SELECT ?s ?p ?o WHERE {
                ?s ?p ?o .
            }
        ";
        var results = _engine.ExecuteQuery(query).Take(1000).ToList();
        return results.Count;
    }

    [Benchmark]
    public int SparqlQueryWithGraphClause()
    {
        var query = @"
            PREFIX ex: <http://example.org/>
            SELECT ?s ?o WHERE {
                GRAPH ex:socialGraph {
                    ?s ex:knows ?o .
                }
            }
        ";
        var results = _engine.ExecuteQuery(query).Take(1000).ToList();
        return results.Count;
    }

    [Benchmark]
    public int SparqlJoinQuery()
    {
        // Find friends of friends (2-hop join)
        var query = @"
            PREFIX ex: <http://example.org/>
            SELECT ?s ?friend ?fof WHERE {
                GRAPH ex:socialGraph {
                    ex:person50 ex:knows ?friend .
                    ?friend ex:knows ?fof .
                }
            }
        ";
        var results = _engine.ExecuteQuery(query).ToList();
        return results.Count;
    }

    [Benchmark]
    public int SparqlQueryWithFilter()
    {
        // Note: Current minimal engine doesn't support FILTER, 
        // so this measures query without filter for baseline
        var query = @"
            PREFIX ex: <http://example.org/>
            SELECT ?s ?age WHERE {
                ?s ex:age ?age .
            }
        ";
        var results = _engine.ExecuteQuery(query).Take(500).ToList();
        return results.Count;
    }

    [Benchmark]
    public int MultipleSparqlQueries()
    {
        int total = 0;
        for (int i = 0; i < 50; i++)
        {
            var query = $@"
                PREFIX ex: <http://example.org/>
                SELECT ?name WHERE {{
                    ex:person{i * 10} ex:name ?name .
                }}
            ";
            var results = _engine.ExecuteQuery(query).ToList();
            total += results.Count;
        }
        return total;
    }
}
