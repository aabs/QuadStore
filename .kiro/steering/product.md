# Product Overview

QuadStore is a high-performance, lightweight in-memory RDF quad store for .NET 10. It stores RDF quads (subject, predicate, object, graph) using columnar storage with dictionary encoding and roaring bitmap indexes for sub-microsecond query latency.

## Key Capabilities

- Single-pass TriG parsing via ANTLR grammar (no external RDF library dependency for loading)
- Roaring bitmap indexes on all four quad dimensions (S, P, O, G) for O(1) lookups
- Basic SPARQL engine with pattern matching and variable binding
- dotNetRDF adapter (`QuadStoreStorageProvider`) enabling Leviathan SPARQL query execution
- Persistence via memory-mapped columnar files (dictionary, columns, bitmap indexes)
- Append-only design — no deletion or SPARQL Update support currently
- Thread-safe via `ReaderWriterLockSlim` for concurrent reads and serialized writes

## Domain Context

- RDF (Resource Description Framework) data model with named graphs
- W3C TriG format for data loading
- SPARQL for querying
- Target use cases: loading and querying RDF datasets at scale with minimal overhead
