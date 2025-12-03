using System;
using System.IO;
using System.Linq;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Engines;
using TripleStore.Core;
using SparqlEngine;

namespace TripleStore.Benchmarks;

/// <summary>
/// End-to-end scalability benchmarks simulating industrial use cases.
/// Tests realistic workloads with mixed operations at large scales.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RunStrategy.Monitoring, iterationCount: 1, warmupCount: 0)]
public class ScalabilityBenchmarks
{
    private string _tempDir = null!;
    private QuadStore _store = null!;
    private MinimalSparqlEngine _engine = null!;

    [Params(100_000, 1_000_000, 10_000_000)]
    public int DatasetSize { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"benchmark_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _store = new QuadStore(_tempDir);
        
        Console.WriteLine($"Loading {DatasetSize:N0} triples...");
        
        // Simulate industrial ontology with entities, relationships, and metadata
        int entityCount = (int)Math.Sqrt(DatasetSize / 5);
        int relationshipsPerEntity = 5;
        
        for (int i = 0; i < entityCount; i++)
        {
            // Entity metadata
            _store.Append(
                $"http://example.org/entity{i}",
                "http://www.w3.org/1999/02/22-rdf-syntax-ns#type",
                $"http://example.org/EntityType{i % 10}",
                "http://example.org/ontologyGraph"
            );
            
            _store.Append(
                $"http://example.org/entity{i}",
                "http://www.w3.org/2000/01/rdf-schema#label",
                $"\"Entity {i}\"",
                "http://example.org/ontologyGraph"
            );
            
            // Properties
            _store.Append(
                $"http://example.org/entity{i}",
                "http://example.org/property/value",
                $"\"{i * 1.5}\"",
                "http://example.org/dataGraph"
            );
            
            _store.Append(
                $"http://example.org/entity{i}",
                "http://example.org/property/timestamp",
                $"\"2025-12-03T{(i % 24):D2}:00:00Z\"",
                "http://example.org/dataGraph"
            );
            
            // Relationships
            for (int j = 0; j < relationshipsPerEntity; j++)
            {
                int targetId = (i + j + 1) % entityCount;
                _store.Append(
                    $"http://example.org/entity{i}",
                    $"http://example.org/relation/connectedTo",
                    $"http://example.org/entity{targetId}",
                    "http://example.org/relationshipGraph"
                );
            }
            
            if ((i + 1) % 10000 == 0)
            {
                Console.WriteLine($"  Loaded {i + 1:N0} entities...");
            }
        }
        
        Console.WriteLine("Loading complete. Initializing engine...");
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

    [Benchmark]
    public void IndustrialWorkload_EntityLookup()
    {
        // Simulate 100 random entity lookups
        var random = new Random(42);
        int lookupCount = 100;
        int totalResults = 0;
        
        for (int i = 0; i < lookupCount; i++)
        {
            int entityId = random.Next((int)Math.Sqrt(DatasetSize / 5));
            var query = $@"
                PREFIX ex: <http://example.org/>
                PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
                SELECT ?label WHERE {{
                    ex:entity{entityId} rdfs:label ?label .
                }}
            ";
            var results = _engine.ExecuteQuery(query).ToList();
            totalResults += results.Count;
        }
    }

    [Benchmark]
    public void IndustrialWorkload_GraphTraversal()
    {
        // Find all entities connected within 2 hops
        var query = @"
            PREFIX ex: <http://example.org/>
            SELECT ?s ?intermediate ?target WHERE {
                GRAPH ex:relationshipGraph {
                    ex:entity100 ex:relation/connectedTo ?intermediate .
                    ?intermediate ex:relation/connectedTo ?target .
                }
            }
        ";
        var results = _engine.ExecuteQuery(query).ToList();
    }

    [Benchmark]
    public void IndustrialWorkload_TypeQuery()
    {
        // Find all entities of a specific type
        var query = @"
            PREFIX ex: <http://example.org/>
            PREFIX rdf: <http://www.w3.org/1999/02/22-rdf-syntax-ns#>
            SELECT ?entity WHERE {
                GRAPH ex:ontologyGraph {
                    ?entity rdf:type ex:EntityType5 .
                }
            }
        ";
        var results = _engine.ExecuteQuery(query).Take(1000).ToList();
    }

    [Benchmark]
    public void IndustrialWorkload_MixedOperations()
    {
        var random = new Random(42);
        int operations = 50;
        
        for (int i = 0; i < operations; i++)
        {
            // 70% reads, 30% writes
            if (random.NextDouble() < 0.7)
            {
                // Read operation
                int entityId = random.Next((int)Math.Sqrt(DatasetSize / 5));
                var results = _store.Query(subject: $"http://example.org/entity{entityId}").ToList();
            }
            else
            {
                // Write operation - add new relationship
                int sourceId = random.Next((int)Math.Sqrt(DatasetSize / 5));
                int targetId = random.Next((int)Math.Sqrt(DatasetSize / 5));
                _store.Append(
                    $"http://example.org/entity{sourceId}",
                    "http://example.org/relation/newConnection",
                    $"http://example.org/entity{targetId}",
                    "http://example.org/relationshipGraph"
                );
            }
        }
    }

    [Benchmark]
    public void IndustrialWorkload_FullPersistence()
    {
        // Measure end-to-end persistence at scale
        _store.SaveAll();
    }
}
