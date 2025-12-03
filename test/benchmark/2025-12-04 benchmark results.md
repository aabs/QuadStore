# 2025-12-04 Benchmark Results

Ran terminal command: `cd benchmark/TripleStore.Benchmarks; dotnet run -c Release --filter "*QuadStoreLoadBenchmarks*" --job short`

## Benchmark Results Summary

### Key Performance Metrics (1M triples)

| Benchmark | Mean Time | Throughput | GC Pressure | Memory |
|-----------|-----------|------------|-------------|---------|
| **SequentialInserts** | **3.80s** | **~263K triples/sec** | 35 Gen0, 34 Gen1, 1 Gen2 | 691 MB |
| SequentialInsertsWithFlush | 8.83s | ~113K triples/sec | 118 Gen0, 45 Gen1, 7 Gen2 | 1,937 MB |
| MultipleGraphsInserts | 3.99s | ~250K triples/sec | 40 Gen0, 39 Gen1, 1 Gen2 | 761 MB |
| VaryingPredicatesInserts | 4.71s | ~212K triples/sec | 41 Gen0, 40 Gen1, 1 Gen2 | 773 MB |
| HighlyConnectedGraph | 3.32s | ~302K triples/sec | 25 Gen0, 24 Gen1, 1 Gen2 | 435 MB |

### Analysis

1. **Throughput**: **263K-302K inserts/second** exceeds the 10K target by **26-30x**
2. **Flush overhead**: Intermediate `SaveAll()` calls increase time by **2.3x** and memory by **2.8x** due to memory-mapped file remapping
3. **HighlyConnectedGraph** is **fastest** - fewer unique subjects (100 nodes vs 1M) reduces dictionary overhead
4. **VaryingPredicatesInserts** is **slowest** due to 100 different predicates increasing dictionary churn

### Recommendations

1. **Batch SaveAll() calls**: Avoid frequent flush operations; batch at end or periodic checkpoints (e.g., every 100K triples)
2. **Dictionary pre-sizing**: If entity counts are known, pre-allocate dictionary capacity
3. **Consider columnar compression**: 1M triples uses ~691 MB (691 bytes/triple) - could benefit from run-length encoding for repeated values
4. **Monitor Gen2 GC**: Large-scale workloads trigger full GC; consider tuning GC settings for sustained throughput

All 214 unit tests passing ✅  
Benchmark project fully operational ✅

Made changes.