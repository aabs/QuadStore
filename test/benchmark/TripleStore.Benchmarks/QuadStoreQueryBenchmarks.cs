using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using TripleStore.Core;

namespace TripleStore.Benchmarks;

/// <summary>
/// Benchmarks for querying QuadStore with various patterns and scales.
/// Tests selectivity, index usage, and scan performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5)]
public class QuadStoreQueryBenchmarks
{
    private string _tempDir = null!;
    private QuadStore _store = null!;

    [Params(10_000, 100_000, 1_000_000)]
    public int DatasetSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"benchmark_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new QuadStore(_tempDir);

        // Load diverse dataset
        int graphCount = 5;
        int predicateCount = 20;
        
        for (int i = 0; i < DatasetSize; i++)
        {
            _store.Append(
                $"http://example.org/subject{i}",
                $"http://example.org/predicate{i % predicateCount}",
                $"http://example.org/object{i}",
                $"http://example.org/graph{i % graphCount}"
            );
        }
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
    public int QueryBySubject()
    {
        // Highly selective - single subject
        var results = _store.Query(subject: "http://example.org/subject100").ToList();
        return results.Count;
    }

    [Benchmark]
    public int QueryByPredicate()
    {
        // Medium selectivity - 5% of data
        var results = _store.Query(predicate: "http://example.org/predicate5").ToList();
        return results.Count;
    }

    [Benchmark]
    public int QueryByObject()
    {
        // Highly selective
        var results = _store.Query(obj: "http://example.org/object500").ToList();
        return results.Count;
    }

    [Benchmark]
    public int QueryByGraph()
    {
        // Low selectivity - 20% of data
        var results = _store.Query(graph: "http://example.org/graph1").ToList();
        return results.Count;
    }

    [Benchmark]
    public int QueryBySubjectAndPredicate()
    {
        // Very high selectivity - intersection
        var results = _store.Query(
            subject: "http://example.org/subject1000",
            predicate: "http://example.org/predicate10"
        ).ToList();
        return results.Count;
    }

    [Benchmark]
    public int QueryByPredicateAndGraph()
    {
        // Medium selectivity - intersection
        var results = _store.Query(
            predicate: "http://example.org/predicate5",
            graph: "http://example.org/graph2"
        ).ToList();
        return results.Count;
    }

    [Benchmark]
    public int QueryAllTriples()
    {
        // Full scan - worst case
        var results = _store.Query().ToList();
        return results.Count;
    }

    [Benchmark]
    public int MultipleSmallQueries()
    {
        int total = 0;
        for (int i = 0; i < 100; i++)
        {
            var results = _store.Query(subject: $"http://example.org/subject{i * 100}").ToList();
            total += results.Count;
        }
        return total;
    }
}
