# TripleStore Performance Benchmarks

Comprehensive benchmarking suite for QuadStore and SPARQL engine performance analysis at industrial scale.

## Benchmark Categories

### 1. **QuadStoreLoadBenchmarks**
Tests raw data loading performance:
- Sequential inserts (baseline)
- Inserts with periodic flush
- Multiple graphs
- Varying predicates
- Highly connected graphs

**Scales:** 1K, 10K, 100K, 1M triples

**Key Metrics:**
- Throughput (ops/sec)
- Memory allocation
- Insert latency

### 2. **QuadStoreQueryBenchmarks**
Tests query performance across selectivity ranges:
- Single-field queries (subject, predicate, object, graph)
- Multi-field queries (intersections)
- Full scans
- Multiple small queries

**Scales:** 10K, 100K, 1M triples

**Key Metrics:**
- Query latency
- Index effectiveness
- Selectivity impact

### 3. **SparqlEngineBenchmarks**
Tests SPARQL query execution:
- Simple patterns
- Multi-pattern joins
- Graph clause filtering
- Friend-of-friend traversals
- Query parsing overhead

**Scales:** 10K, 100K triples

**Key Metrics:**
- End-to-end query time
- Join performance
- Graph filtering overhead

### 4. **PersistenceBenchmarks**
Tests durability and I/O performance:
- SaveAll operations
- LoadAll operations
- Save and reload cycles
- Mixed read/write with periodic saves

**Scales:** 10K, 100K, 1M triples

**Key Metrics:**
- Save throughput
- Load time
- Durability overhead

### 5. **TriGLoaderBenchmarks**
Tests TriG file loading:
- Small files (1K triples)
- Medium files (10K triples)
- Large files (100K triples)
- String-based loading

**Key Metrics:**
- Parsing throughput
- Bulk insert performance
- File I/O overhead

### 6. **ScalabilityBenchmarks**
End-to-end industrial workload simulations:
- Entity lookups (random access)
- Graph traversal (2-hop joins)
- Type-based queries
- Mixed read/write operations
- Full persistence cycles

**Scales:** 100K, 1M, 10M triples

**Key Metrics:**
- Real-world latency
- Throughput under load
- Scalability limits

## Running Benchmarks

### Run All Benchmarks
```powershell
cd test\benchmark\TripleStore.Benchmarks
dotnet run -c Release
```

### Run Specific Benchmark
```powershell
dotnet run -c Release --filter "*QuadStoreLoadBenchmarks*"
```

### Run Single Method
```powershell
dotnet run -c Release --filter "*SequentialInserts*"
```

### Run with Custom Parameters
```powershell
dotnet run -c Release --filter "*QuadStoreLoadBenchmarks*" --job short
```

## Analyzing Results

### Output Files
Benchmarks generate results in `BenchmarkDotNet.Artifacts/results/`:
- `*-report.html` - Interactive HTML report
- `*-report.csv` - CSV data for analysis
- `*-report-github.md` - Markdown summary
- `*.log` - Detailed execution logs

### Key Metrics to Watch

**For Industrial Scale (1M+ triples):**
- **Load throughput:** Should sustain >10K inserts/sec
- **Query latency:** Single-entity lookup <10ms
- **Join performance:** 2-hop traversal <100ms
- **Memory:** <2GB for 1M triples
- **Persistence:** SaveAll <5 seconds for 1M triples

**Performance Targets:**
| Operation | Target | Alert Threshold |
|-----------|--------|-----------------|
| Insert | >10K/sec | <5K/sec |
| Point query | <10ms | >50ms |
| Range query (5%) | <50ms | >200ms |
| Join (2-hop) | <100ms | >500ms |
| Save 1M triples | <5s | >15s |
| Load 1M triples | <3s | >10s |

### Memory Diagnostics
Enable memory profiler for detailed allocation analysis:
```powershell
dotnet run -c Release --filter "*" --memory
```

### CPU Profiler (Windows only)
```powershell
dotnet run -c Release --filter "*" --profiler ETW
```

## Interpreting Results

### Good Performance Indicators
- Linear scaling with data size
- Consistent latency across runs
- Low memory allocation per operation
- High cache hit rates

### Performance Concerns
- Exponential scaling (O(n²) or worse)
- High standard deviation
- Excessive GC pressure
- Full scans for selective queries

## Optimization Workflow

1. **Baseline:** Run full suite to establish baseline
2. **Profile:** Identify bottlenecks using memory/CPU profilers
3. **Optimize:** Target specific hot paths
4. **Verify:** Re-run affected benchmarks
5. **Regress:** Ensure no performance regressions

## Continuous Monitoring

### CI Integration
Add to GitHub Actions or Azure DevOps:
```yaml
- name: Run Benchmarks
  run: |
    cd benchmark/TripleStore.Benchmarks
    dotnet run -c Release --filter "*QuadStoreLoadBenchmarks.SequentialInserts*" --exporters json
```

### Performance Tracking
- Store results in git (BenchmarkDotNet.Artifacts/)
- Track trends over time
- Alert on regressions >10%

## Customization

### Add New Benchmark
1. Create class in `benchmark/TripleStore.Benchmarks/`
2. Add `[MemoryDiagnoser]` attribute
3. Define `[Params]` for scales
4. Implement `[Benchmark]` methods
5. Run and analyze

### Adjust Scales
Modify `[Params]` values based on your use case:
```csharp
[Params(100, 1_000, 10_000)]  // Smaller scale
[Params(10_000_000)]           // Single large scale
```

## Known Limitations

- **Current engine:** Minimal SPARQL support (no FILTER, OPTIONAL, UNION)
- **Joins:** Only basic nested loop joins
- **Indexes:** Four indexes (S, P, O, G) - no composite indexes
- **Persistence:** Full save/load only - no incremental updates

## Future Enhancements

- [ ] Add concurrency benchmarks (multi-threaded access)
- [ ] Test with real-world RDF datasets (DBpedia, Wikidata samples)
- [ ] Benchmark index building time
- [ ] Test query optimization strategies
- [ ] Measure memory fragmentation over time
- [ ] Add compression benchmarks
