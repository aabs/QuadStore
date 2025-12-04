# QuadStore Performance Context and Positioning

## Executive Summary

This document provides evidence-based context for QuadStore benchmark results based on:
- Published academic benchmark frameworks (BSBM, SP2Bench, WatDiv, LUBM)
- W3C RDF Store Benchmarking guidelines
- Comparative analysis framework from TheWebConf2019 paper
- Current QuadStore benchmark results

## Current QuadStore Performance Profile

### Query Performance (1M triple scale)
| Operation | Latency | Notes |
|-----------|---------|-------|
| Subject lookup | 138 ns | Single-index bitmap intersection |
| Object lookup | 198 ns | Single-index bitmap intersection |
| Predicate scan (5%) | 4.4 ms | Full scan with bitmap filtering |
| Subject+Predicate | 30.4 µs | Two-index intersection |
| Subject+Object | 7.6 µs | Two-index intersection |

### Load Performance (1M triples)
| Pattern | Time | Throughput | Notes |
|---------|------|------------|-------|
| Sequential inserts | 3.29 s | 304K triples/s | Baseline |
| Periodic flush | 4.44 s | 225K triples/s | Write-through mode |
| Multiple graphs (10) | 3.41 s | 293K triples/s | Graph switching overhead |
| High cardinality predicates | 3.93 s | 254K triples/s | 20 distinct predicates |
| Highly connected graph | 4.64 s | 215K triples/s | Many-to-many relationships |

### Persistence Performance (1M triples)
| Operation | Time | Throughput |
|-----------|------|------------|
| Save | 840 ms | 1.19M triples/s |
| Load | 4.72 s | 212K triples/s |
| Round-trip | 5.56 s | - |

## Academic Benchmark Context

### Standard Benchmark Frameworks

Based on W3C RDF Store Benchmarking documentation and dice-group/triplestore-benchmarks analysis:

**Berlin SPARQL Benchmark (BSBM)**
- Domain: E-commerce use case
- Scales: 100K - 150B triples
- Query types: Explore, update, business intelligence
- Third-party results from 2013 (Boncz & Pham) compared Virtuoso, Jena TDB, BigData, BigOWLIM
- **Note:** QuadStore has not been tested against BSBM query workloads

**SP2Bench (SPARQL Performance Benchmark)**
- Domain: DBLP publications dataset
- Focus: SPARQL operator constellations (joins, unions, optionals, filters)
- Synthetic scalable data generator
- Tests used by commercial stores (Virtuoso, Jena, Stardog) in published comparisons
- **Note:** QuadStore benchmarks focus on low-level query patterns, not complex SPARQL operators

**WatDiv (Waterloo SPARQL Diversity Test Suite)**
- Domain: Stress testing framework
- Highly customizable query diversity
- Used in ESWC 2023 Wikidata benchmark (16B triples, GraphDB/Jena/Neptune/RDFox/Stardog/QLever)
- **Note:** QuadStore has not been evaluated with WatDiv workloads

**LUBM (Lehigh University Benchmark)**
- Domain: University ontology with inference
- Focus: Reasoning and scalability
- Used extensively by commercial stores (Virtuoso, AllegroGraph, GraphDB) for published results
- **Note:** QuadStore does not implement reasoning

### Comparative Analysis Challenges

From dice-group/triplestore-benchmarks (TheWebConf2019):

**Key finding:** "Existing triplestore benchmarks vary significantly in:"
1. Dataset structuredness
2. Relationship specialty (graph connectivity patterns)
3. Query diversity (SPARQL clause usage: LIMIT, OPTIONAL, ORDER BY, DISTINCT, UNION, FILTER, REGEX)
4. Query feature correlation with runtime (triple patterns, selectivity, join vertices)

**Implication:** Direct performance comparisons across different benchmarks are not meaningful without:
- Same dataset scale and characteristics
- Same query workload
- Same hardware configuration
- Same measurement methodology

## Performance Positioning: Evidence-Based Assessment

### What QuadStore Benchmarks Demonstrate

**Strengths:**
1. **Fast indexed lookups:** 138-198ns for single-entity queries at 1M scale
   - Bitmap intersection over integer ordinals
   - Efficient memory layout for common access patterns

2. **Reasonable load throughput:** 215K-304K triples/sec sustained
   - In-memory operation with no ACID overhead
   - Simple append-only model

3. **Efficient persistence:** 1.19M triples/sec save, 5.6s round-trip for 1M triples
   - Custom binary serialization
   - Memory-mapped file handling

**Current Limitations:**
1. **No published comparison against commercial stores** (Virtuoso, GraphDB, Stardog, Blazegraph, Jena)
2. **Limited query workload coverage:**
   - No BSBM/SP2Bench/WatDiv query execution
   - No complex SPARQL operator testing (UNION, OPTIONAL, aggregation, subqueries)
   - No reasoning/inference evaluation
3. **Small scale testing:** Maximum 1M-10M triples vs. industry standard 100M-16B
4. **In-memory only:** No disk-based scalability testing
5. **Single-threaded:** No concurrent query workload evaluation

### Comparison Methodology Gap

**To provide valid comparative claims, QuadStore would need:**

1. **Standard benchmark execution:**
   - Run BSBM Explore/BI query mix at 100M-1B triple scales
   - Execute SP2Bench full query set on DBLP-scale data
   - WatDiv diversity workload with same configurations used in published papers

2. **Published baseline results:**
   - Virtuoso BSBM results (available from OpenLink)
   - Jena TDB SP2Bench published numbers
   - GraphDB LDBC-SNB results from GraphDB website
   - Compare on identical hardware, scales, and configurations

3. **Third-party validation:**
   - Independent benchmark execution
   - Reproducible configuration
   - Full disclosure report (TPC-style)

## Appropriate Performance Claims

### ✅ Supported Claims

"QuadStore achieves **sub-microsecond query latency** for indexed single-entity lookups at 1M triple scale (138ns-198ns measured with BenchmarkDotNet)."

"Load throughput of **215K-304K triples/second** sustained across diverse insert patterns (sequential, multi-graph, high-cardinality) at 1M scale."

"Persistence overhead of **<1 second save time** for 1M triples with custom binary serialization."

"Roaring bitmap indexes enable **two-index intersection queries in 7-30µs** for highly selective patterns."

### ❌ Unsupported Claims

"World-class query performance" - **No comparative data**

"Competitive with Virtuoso/GraphDB" - **No benchmark execution on same workloads**

"Production-ready at web scale" - **Maximum tested scale 10M triples; no BSBM/SP2Bench validation**

"SPARQL-compliant performance" - **Minimal SPARQL operator coverage in benchmarks**

## Recommendations for Future Benchmarking

### Phase 1: Standard Benchmark Execution
1. Implement BSBM query driver integration
2. Run BSBM Explore use case at 100M, 1B triple scales
3. Document configuration, hardware, methodology
4. Compare against published Virtuoso/Jena/GraphDB BSBM results

### Phase 2: SPARQL Operator Coverage
1. Execute SP2Bench full query set
2. Test complex SPARQL operators (UNION, OPTIONAL, ORDER BY, aggregation)
3. Measure parsing overhead vs. execution time
4. Identify optimization opportunities for complex queries

### Phase 3: Scalability Validation
1. Test at 100M, 1B, 10B triple scales
2. Disk-based storage evaluation (vs. in-memory only)
3. Concurrent query workload (multiple clients)
4. Multi-threaded insert performance

### Phase 4: Independent Validation
1. Publish reproducible benchmark configurations
2. Provide Docker/container-based benchmark setup
3. Submit results to HOBBIT platform or similar
4. Seek third-party benchmark execution

## References

### Academic Sources
- Atemezing & Amardeilh (2018): "Benchmarking Commercial RDF Stores with Publications Office Dataset" (ESWC 2018)
- Saleem et al. (2019): "How Representative is a SPARQL Benchmark? An Analysis of RDF Triplestore Benchmarks" (TheWebConf2019)
- Boncz & Pham (2013): "Berlin SPARQL Benchmark Results for Virtuoso, Jena TDB, BigData, and BigOWLIM"

### Benchmark Frameworks
- W3C RDF Store Benchmarking: https://www.w3.org/wiki/RdfStoreBenchmarking
- BSBM: http://wifo5-03.informatik.uni-mannheim.de/bizer/berlinsparqlbenchmark/
- SP2Bench: http://dbis.informatik.uni-freiburg.de/index.php?project=SP2B
- WatDiv: http://db.uwaterloo.ca/watdiv/
- LUBM: http://swat.cse.lehigh.edu/projects/lubm/

### Open Data
- dice-group/triplestore-benchmarks: https://github.com/dice-group/triplestore-benchmarks
  - Contains datasets and queries for 10 benchmarks + 5 real-world datasets
  - Provides comparative analysis framework
  - Benchmark utilities for structuredness, diversity, correlation analysis

## Conclusion

QuadStore demonstrates **efficient in-memory RDF query performance** with sub-microsecond indexed lookups and competitive load throughput for its current scale (1M-10M triples) and feature set (basic SPARQL patterns).

**However, without execution of standard benchmarks (BSBM, SP2Bench, WatDiv) and comparison against published results from commercial stores, performance claims must be limited to the specific micro-benchmarks executed.**

The benchmark results provide a **solid foundation for optimization work** and demonstrate the benefits of Roaring bitmap indexes for intersection queries. To position QuadStore relative to industry standards, the next step is executing one or more standard benchmark suites and comparing against published baselines.

---
*Last updated: 2025-12-04*
*Benchmark execution environment: .NET 10.0, Windows 11, Intel Core Ultra 7 265H*
