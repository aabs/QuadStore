# Tech Stack

- Language: C# (latest LangVersion)
- Framework: .NET 10.0
- Build system: MSBuild / `dotnet` CLI
- Solution file: `QuadStore.sln`
- IDE: Visual Studio 2022+

## Key Dependencies

### QuadStore.Core (main library)
- `Antlr4.Runtime.Standard` 4.13.1 — TriG lexer/parser runtime
- `dotNetRDF` 3.4.1 — RDF types and Leviathan SPARQL engine (used in adapter layer)
- `Roaring.Net` 1.0.0 — Roaring bitmap indexes
- `Vogen` 8.0.3 — Value object generation

### QuadStore.Storage (separate module, not used by Core)
- `LightningDB` 0.20.0 — LMDB-based persistence (experimental, not integrated with QuadStore.Core)

### Test project
- `xunit` 2.6.6 — Test framework
- `FluentAssertions` 8.8.0 — Assertion library
- `AutoFixture` 4.18.1 + `AutoFixture.Xunit2` — Test data generation
- `coverlet.collector` 6.0.4 — Code coverage

### Benchmarks
- `BenchmarkDotNet` 0.14.0

## ANTLR Parser Generation

The TriG parser is generated from `.g4` grammar files at build time via an MSBuild `InitialTargets` step. Requires Java on PATH to run the ANTLR tool (`tools/antlr-4.13.2-complete.jar`). Generated C# files go into `src/QuadStore.Core/trig-grammar/`.

## Common Commands

```bash
# Build the solution
dotnet build -c Release

# Run all tests
dotnet test -c Release --nologo

# Run benchmarks
dotnet run -c Release --project benchmark/TripleStore.Benchmarks/TripleStore.Benchmarks.csproj
```

## Code Style

- EditorConfig enforced (`.editorconfig` at repo root)
- 4-space indentation for C# files
- LF line endings
- UTF-8 with BOM for C# files
- `var` preferred when type is apparent
- Allman-style braces (new line before open brace)
- System usings sorted first
- Nullable reference types: disabled in Core, enabled in Storage and Tests
