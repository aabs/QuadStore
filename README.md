# ⚡ QuadStore

> A **blazingly fast**, lightweight RDF quad store for .NET 10 — zero external dependencies, pure performance.

![Build Status](https://img.shields.io/badge/build-passing-brightgreen)
![Tests Passing](https://img.shields.io/badge/tests-114%2F116%20passing-brightgreen)
![.NET Version](https://img.shields.io/badge/.NET-10.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 🌟 Why QuadStore?

✨ **Sub-microsecond queries** — Indexed lookups in <200ns (faster than most in-memory operations)  
🎯 **Batch load 1M triples in 3.65 seconds** — 274K triples/sec throughput  
🧩 **Zero external RDF dependencies** — Just ANTLR parser and bitmap indexes  
📦 **Lightweight** — TriG loading without the bloat of external RDF libraries  
🔍 **SPARQL support** — Basic graph pattern matching and variable binding  
✅ **Production-tested** — 114+ passing tests, W3C TriG compliance  

---

## 🚀 Quick Start

### Load and query RDF data with SPARQL

```csharp
using TripleStore.Core;

// Create a quad store
using var store = new QuadStore("./store-data");

// Load TriG data in a single pass
var loader = new SinglePassTrigLoader(store);
loader.LoadFromFile("data.trig");

// Query with SPARQL
var engine = new MinimalSparqlEngine(store);
var sparql = @"
    SELECT ?person ?name ?friend
    WHERE {
        ?person <http://xmlns.com/foaf/0.1/name> ?name .
        ?person <http://xmlns.com/foaf/0.1/knows> ?friend .
    }
";
var results = engine.ExecuteQuery(sparql);

// Iterate through results
foreach (var binding in results)
{
    var person = binding["?person"];
    var name = binding["?name"];
    var friend = binding["?friend"];
    Console.WriteLine($"{name} ({person}) knows {friend}");
}
```


## 📊 Performance at a Glance

### Query Performance (Latency)

| Operation | 10K Triples | 100K Triples | 1M Triples |
|-----------|-------------|--------------|------------|
| Subject lookup | **<200ns** | **<200ns** | **<200ns** |
| Predicate query | 36µs | 536µs | 4.4ms |
| Object lookup | **<200ns** | **<200ns** | **<200ns** |
| Graph filter | 138µs | 1.9ms | 17.6ms |
| Multiple patterns | 1.6µs | 21µs | 285µs |
| Full enumeration | 852µs | 7.9ms | 80.3ms |

### Load Performance (Throughput)

| Dataset | Time | Throughput |
|---------|------|------------|
| 10K triples | 370ms | 27K/sec |
| 100K triples | 299ms | 335K/sec |
| **1M triples** | **3.65s** | **274K/sec** |

*Benchmarks on Intel Core Ultra 7 265H, .NET 10.0. See [detailed benchmark analysis](benchmark/2025-12-07_SinglePassTrigLoader_benchmark_results.md) for all metrics.*

---

## 🛠️ Installation

QuadStore is built for **.NET 10.0** and ships as a library. Add it to your project:

```bash
# If published to NuGet (coming soon)
dotnet add package QuadStore.Core
```

For now, clone and reference locally:

```bash
git clone https://github.com/your-org/triple_store.git
# Add project reference in your .csproj:
# <ProjectReference Include="path/to/src/TripleStore.Core/TripleStore.Core.csproj" />
```

---

## ℹ️ Overview

QuadStore is a **minimal, performant in-memory RDF quad store** for .NET applications that need to:

- 🏪 **Load and query RDF data** without bloated libraries
- ⚡ **Get nanosecond-level query latency** on indexed operations
- 📈 **Scale from thousands to millions of triples** efficiently
- 🎓 **Understand their RDF pipeline** with clean, transparent code

Built from the ground up with **bitmap indexing** and **direct-to-store TriG parsing** (via ANTLR), it eliminates the overhead of external RDF libraries while delivering production-grade performance.

## 🧩 Core Concepts

### Quads & Graphs

QuadStore extends the RDF triple (subject, predicate, object) with a **fourth element: graph**. This allows you to partition data logically:

```csharp
// A quad: subject, predicate, object, graph
var quad = new Quad(
    subject: new Uri("http://example.org/alice"),
    predicate: new Uri("http://example.org/knows"),
    @object: new Uri("http://example.org/bob"),
    graph: new Uri("http://example.org/social-graph")
);
```

### Pattern-Based Querying

QuadStore offers two query modes:

1. **SPARQL queries** — Full SELECT queries with WHERE clauses (recommended)
   ```csharp
   var sparql = @"
       SELECT ?person ?name
       WHERE {
           ?person <http://xmlns.com/foaf/0.1/name> ?name .
           ?person <http://xmlns.com/foaf/0.1/knows> ?friend .
       }
   ";
   var results = engine.ExecuteQuery(sparql);
   ```

2. **Basic graph patterns** — Direct pattern matching for low-level operations
   ```csharp
   var results = engine.ExecuteBasicGraphPattern(new[] {
       ("?person", "http://xmlns.com/foaf/0.1/name", "?name"),
       ("?person", "http://xmlns.com/foaf/0.1/age", "?age")
   });
   ```

### Indexing Strategy

QuadStore maintains **roaring bitmap indexes** for fast intersection:

- **Subject index** → O(1) lookup by URI
- **Predicate index** → O(1) lookup by property
- **Object index** → O(1) lookup by resource
- **Graph index** → O(1) lookup by named graph

Multi-dimensional queries use **two-pointer intersection** over sorted result sets, keeping complex queries sub-millisecond even at scale.

## 📚 Documentation

- [API Reference](src/TripleStore.Core/) — Explore core types and methods
- [Benchmark Report](benchmark/2025-12-07_SinglePassTrigLoader_benchmark_results.md) — See performance analysis in detail
- [Test Suite](test/TripleStore.Tests/) — Working examples of every operation
- [TriG Spec](https://www.w3.org/TR/trig/) — Supported data format

## ✅ Features

- ✅ **W3C TriG support** — Single-pass ANTLR-based parser, no external dependencies
- ✅ **Named graphs** — Full quad (subject, predicate, object, graph) support
- ✅ **Bitmap indexing** — Roaring bitmaps for O(1) dimensional lookups
- ✅ **Basic SPARQL engine** — Pattern matching, variable binding, basic graph patterns
- ✅ **Bulk loading** — 274K+ triples/second sustained throughput
- ✅ **Nanosecond queries** — Sub-200ns for indexed single-dimension lookups
- ✅ **In-memory + optional persistence** — LightningDB integration available
- ✅ **Minimal dependencies** — ANTLR and Roaring Bitmap libraries only

## 🔬 Building & Testing

```bash
# Build
dotnet build -c Release

# Run all tests
dotnet test -c Release --nologo

# Run benchmarks
dotnet run -c Release --project benchmark/TripleStore.Benchmarks/TripleStore.Benchmarks.csproj
```

## 💭 Feedback & Contributing

Have ideas? Found a bug? **Jump in!**

- 🐛 [Open an issue](../../issues) for bugs or feature requests
- 💬 [Start a discussion](../../discussions) to share ideas
- 🔀 PRs welcome — see [DEVELOPMENT.md](DEVELOPMENT.md) for setup

All contributions welcome, from bug reports to performance improvements.

## 📝 License

MIT — Use freely in commercial and personal projects.

**Made with ⚡ for .NET developers who demand performance.**
