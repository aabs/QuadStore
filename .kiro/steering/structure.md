# Project Structure

```
QuadStore.sln
├── src/
│   ├── QuadStore.Core/          # Main library — quad store, indexes, TriG loader, SPARQL engine
│   │   ├── QuadStore.cs         # Core quad store: columnar storage, bitmap-indexed query
│   │   ├── BitmapIndex.cs       # Roaring bitmap index (dictId → row IDs)
│   │   ├── DictionaryEncoder.cs # String ↔ int dictionary encoding with persistence
│   │   ├── EncodedColumn.cs     # Memory-mapped int column for S/P/O/G
│   │   ├── SinglePassTrigLoader.cs  # ANTLR visitor that loads TriG directly into QuadStore
│   │   ├── MinimalSparqlEngine.cs   # Basic SPARQL SELECT/BGP engine over QuadStore
│   │   ├── QuadStoreStorageProvider.cs # dotNetRDF IStorageProvider adapter
│   │   ├── TrigLexer.g4 / TrigParser.g4  # ANTLR grammar files
│   │   └── trig-grammar/        # Generated ANTLR C# parser (do not edit)
│   ├── QuadStore.Storage/       # Separate experimental storage module (LightningDB/LMDB, not used by Core)
│   └── TripleStore.Core/        # Legacy/empty project (not actively used)
├── test/
│   └── QuadStore.Tests/         # xUnit test project
│       ├── QuadStoreTests.cs
│       ├── BimapIndexTests.cs
│       ├── DictionaryEncoderTests.cs
│       ├── SinglePassTrigLoaderTests.cs
│       ├── MinimalSparqlEngine*Tests.cs
│       ├── QuadStoreStorageProviderTests.cs
│       ├── TriGLoader*Tests.cs
│       └── test-data/           # Embedded TriG test data files
├── benchmark/
│   └── TripleStore.Benchmarks/  # BenchmarkDotNet performance tests
├── samples/
│   └── LoadTrigFileIntoStore/   # Sample console app
└── tools/                       # ANTLR jar for parser generation
```

## Namespace Convention

- Core library uses namespace `TripleStore.Core` (historical name, project is now called QuadStore)
- Storage library uses namespace `TripleStore.Storage`
- Tests use namespace `TripleStore.Tests`

## Architecture Layers

1. **Storage layer** — `DictionaryEncoder`, `EncodedColumn`, `BitmapIndex` provide columnar persistence and indexing primitives
2. **Quad store** — `QuadStore` orchestrates columns and indexes, exposes `Append()` and `Query()` with bitmap intersection
3. **TriG loading** — `SinglePassTrigLoader` uses ANTLR visitor to parse TriG and call `QuadStore.Append()` directly
4. **SPARQL engine** — `MinimalSparqlEngine` translates SPARQL patterns into `QuadStore.Query()` calls with variable binding
5. **dotNetRDF adapter** — `QuadStoreStorageProvider` implements `IStorageProvider` / `IQueryableStorage` for Leviathan integration

## Key Patterns

- Append-only data model — no delete or update operations on quads
- Dictionary encoding — all string values mapped to int IDs for compact columnar storage
- Bitmap intersection — multi-field queries intersect roaring bitmaps using two-pointer scan
- Thread safety via `ReaderWriterLockSlim` — concurrent reads, exclusive writes
- Tests use temp directories (`Path.GetTempPath()`) for isolated store instances
