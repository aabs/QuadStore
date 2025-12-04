using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using TripleStore.Core;

namespace TripleStore.Benchmarks;

/// <summary>
/// Benchmarks for persistence operations: SaveAll, LoadAll, and mixed read/write scenarios.
/// Tests I/O performance and durability overhead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 3)]
public class PersistenceBenchmarks
{
    private string _tempDir = null!;
    private string _workDir = null!;
    private QuadStore _store = null!;

    [Params(10_000, 100_000, 1_000_000)]
    public int DatasetSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"benchmark_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [IterationSetup]
    public void IterationSetup()
    {
        _workDir = Path.Combine(_tempDir, $"iter_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_workDir);
        _store = new QuadStore(_workDir);
        
        // Pre-load data for save benchmarks
        for (int i = 0; i < DatasetSize; i++)
        {
            _store.Append(
                $"http://example.org/subject{i}",
                "http://example.org/predicate",
                $"http://example.org/object{i}",
                "http://example.org/graph1"
            );
        }
    }

    [IterationCleanup]
    public void IterationCleanup()
    {
        _store.Dispose();
        
        // Clean up iteration directory; ignore file-in-use errors
        try
        {
            if (Directory.Exists(_workDir))
            {
                foreach (var file in Directory.GetFiles(_workDir))
                {
                    try { File.Delete(file); } catch { /* ignore */ }
                }
                Directory.Delete(_workDir, true);
            }
        }
        catch { /* ignore */ }
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
    public void SaveAll()
    {
        _store.SaveAll();
    }

    [Benchmark]
    public void SaveAndReload()
    {
        _store.SaveAll();
        _store.Dispose();
        _store = new QuadStore(_workDir);
        _store.LoadAll();
    }

    [Benchmark]
    public void LoadFromDisk()
    {
        // Pre-save data
        _store.SaveAll();
        _store.Dispose();
        
        // Measure load time
        _store = new QuadStore(_workDir);
        _store.LoadAll();
    }

    [Benchmark]
    public void ContinuousWriteWithPeriodicSave()
    {
        int writeCount = DatasetSize / 10;
        int saveInterval = writeCount / 5;
        
        for (int i = 0; i < writeCount; i++)
        {
            _store.Append(
                $"http://example.org/newsubject{i}",
                "http://example.org/newpredicate",
                $"http://example.org/newobject{i}",
                "http://example.org/graph2"
            );
            
            if ((i + 1) % saveInterval == 0)
            {
                _store.SaveAll();
            }
        }
    }

    [Benchmark]
    public void MixedReadWrite()
    {
        int operations = 1000;
        
        for (int i = 0; i < operations; i++)
        {
            // Write
            _store.Append(
                $"http://example.org/mixed{i}",
                "http://example.org/predicate",
                $"http://example.org/value{i}",
                "http://example.org/graph1"
            );
            
            // Read every 10 writes
            if (i % 10 == 0)
            {
                var results = _store.Query(subject: $"http://example.org/subject{i}").ToList();
            }
        }
    }
}
