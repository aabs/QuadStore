using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using TripleStore.Core;

namespace TripleStore.Benchmarks;

/// <summary>
/// Benchmarks for TriG file loading at various scales.
/// Tests parsing overhead and bulk insert performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Throughput, iterationCount: 3)]
public class TriGLoaderBenchmarks
{
    private string _tempDir = null!;
    private string _smallTrigFile = null!;
    private string _mediumTrigFile = null!;
    private string _largeTrigFile = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"benchmark_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        
        // Generate TriG files of various sizes
        _smallTrigFile = Path.Combine(_tempDir, "small.trig");
        GenerateTriGFile(_smallTrigFile, 1_000);
        
        _mediumTrigFile = Path.Combine(_tempDir, "medium.trig");
        GenerateTriGFile(_mediumTrigFile, 10_000);
        
        _largeTrigFile = Path.Combine(_tempDir, "large.trig");
        GenerateTriGFile(_largeTrigFile, 100_000);
    }

    private void GenerateTriGFile(string path, int tripleCount)
    {
        using var writer = new StreamWriter(path);
        writer.WriteLine("@prefix ex: <http://example.org/> .");
        writer.WriteLine();
        
        int graphCount = 10;
        int triplesPerGraph = tripleCount / graphCount;
        
        for (int g = 0; g < graphCount; g++)
        {
            writer.WriteLine($"ex:graph{g} {{");
            
            for (int i = 0; i < triplesPerGraph; i++)
            {
                int tripleId = g * triplesPerGraph + i;
                writer.WriteLine($"    ex:subject{tripleId} ex:predicate ex:object{tripleId} .");
            }
            
            writer.WriteLine("}");
            writer.WriteLine();
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

    [Benchmark]
    public void LoadSmallTriGFile()
    {
        var storeDir = Path.Combine(_tempDir, $"store_{Guid.NewGuid():N}");
        using var store = new QuadStore(storeDir);
        var loader = new SinglePassTrigLoader(store);
        
        loader.LoadFromFile(_smallTrigFile);
        
        Directory.Delete(storeDir, true);
    }

    [Benchmark]
    public void LoadMediumTriGFile()
    {
        var storeDir = Path.Combine(_tempDir, $"store_{Guid.NewGuid():N}");
        using var store = new QuadStore(storeDir);
        var loader = new SinglePassTrigLoader(store);
        
        loader.LoadFromFile(_mediumTrigFile);
        
        Directory.Delete(storeDir, true);
    }

    [Benchmark]
    public void LoadLargeTriGFile()
    {
        var storeDir = Path.Combine(_tempDir, $"store_{Guid.NewGuid():N}");
        using var store = new QuadStore(storeDir);
        var loader = new SinglePassTrigLoader(store);
        
        loader.LoadFromFile(_largeTrigFile);
        
        Directory.Delete(storeDir, true);
    }

    [Benchmark]
    public void LoadFromString()
    {
        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:s1 ex:p1 ex:o1 .
                ex:s2 ex:p2 ex:o2 .
                ex:s3 ex:p3 ex:o3 .
            }
            
            ex:graph2 {
                ex:s4 ex:p4 ex:o4 .
                ex:s5 ex:p5 ex:o5 .
            }
        ";
        
        var storeDir = Path.Combine(_tempDir, $"store_{Guid.NewGuid():N}");
        using var store = new QuadStore(storeDir);
        var loader = new SinglePassTrigLoader(store);
        
        loader.LoadFromString(trigContent);
        
        Directory.Delete(storeDir, true);
    }
}
