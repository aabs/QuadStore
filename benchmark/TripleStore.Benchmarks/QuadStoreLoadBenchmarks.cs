using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using TripleStore.Core;

namespace TripleStore.Benchmarks;

/// <summary>
/// Benchmarks for loading data into QuadStore at various scales.
/// Tests raw insert performance, batch operations, and persistence overhead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 5)]
public class QuadStoreLoadBenchmarks
{
    private string _tempDir = null!;
    private QuadStore _store = null!;

    [Params(1_000, 10_000, 100_000, 1_000_000)]
    public int TripleCount { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"benchmark_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _store = new QuadStore(_tempDir);
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _store.Dispose();
        // Clean up files between iterations
        foreach (var file in Directory.GetFiles(_tempDir))
        {
            File.Delete(file);
        }
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
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
    public void SequentialInserts()
    {
        for (int i = 0; i < TripleCount; i++)
        {
            _store.Append(
                $"http://example.org/subject{i}",
                "http://example.org/predicate",
                $"http://example.org/object{i}",
                "http://example.org/graph1"
            );
        }
    }

    [Benchmark]
    public void SequentialInsertsWithIntermediateFlush()
    {
        int flushInterval = Math.Max(1000, TripleCount / 10);
        for (int i = 0; i < TripleCount; i++)
        {
            _store.Append(
                $"http://example.org/subject{i}",
                "http://example.org/predicate",
                $"http://example.org/object{i}",
                "http://example.org/graph1"
            );
            
            if ((i + 1) % flushInterval == 0)
            {
                _store.SaveAll();
            }
        }
    }

    [Benchmark]
    public void MultipleGraphsInserts()
    {
        int graphCount = 10;
        for (int i = 0; i < TripleCount; i++)
        {
            _store.Append(
                $"http://example.org/subject{i}",
                "http://example.org/predicate",
                $"http://example.org/object{i}",
                $"http://example.org/graph{i % graphCount}"
            );
        }
    }

    [Benchmark]
    public void VaryingPredicatesInserts()
    {
        int predicateCount = 20;
        for (int i = 0; i < TripleCount; i++)
        {
            _store.Append(
                $"http://example.org/subject{i}",
                $"http://example.org/predicate{i % predicateCount}",
                $"http://example.org/object{i}",
                "http://example.org/graph1"
            );
        }
    }

    [Benchmark]
    public void HighlyConnectedGraph()
    {
        // Simulate a highly connected graph (each subject connects to multiple objects)
        int subjectCount = TripleCount / 10;
        int objectsPerSubject = 10;
        
        for (int s = 0; s < subjectCount; s++)
        {
            for (int o = 0; o < objectsPerSubject; o++)
            {
                _store.Append(
                    $"http://example.org/subject{s}",
                    "http://example.org/knows",
                    $"http://example.org/object{s * objectsPerSubject + o}",
                    "http://example.org/socialGraph"
                );
            }
        }
    }
}
