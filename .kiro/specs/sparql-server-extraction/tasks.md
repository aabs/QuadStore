# Implementation Plan: SPARQL Server Library Extraction

## Overview

Extract the SPARQL HTTP server logic from the sample app into a reusable library at `src/QuadStore.SparqlServer/`, simplify the sample to consume it, and extend test coverage for SPARQL Update, Graph Store Protocol, and additional query forms. Tasks are ordered: project scaffolding → library source → sample refactoring → test retargeting and new tests.

## Tasks

- [ ] 1. Scaffold the new library project and update the solution file
  - [ ] 1.1 Create `src/QuadStore.SparqlServer/QuadStore.SparqlServer.csproj`
    - Use `Microsoft.NET.Sdk`, target `net10.0`, enable `ImplicitUsings` and `Nullable`
    - Add `<FrameworkReference Include="Microsoft.AspNetCore.App" />`
    - Add `<ProjectReference Include="..\QuadStore.Core\QuadStore.Core.csproj" />`
    - _Requirements: 1.1, 1.2, 1.3, 1.4_

  - [ ] 1.2 Add `QuadStore.SparqlServer` to `QuadStore.sln` under the `Src` solution folder
    - Add a new project entry with GUID `{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}` type and a unique project GUID
    - Add Debug and Release build configurations for the new project
    - Nest the project under the `Src` folder GUID `{74CA7E3C-32C1-46DE-8F0C-DDD263815006}`
    - _Requirements: 8.1, 8.2, 8.3_

- [ ] 2. Checkpoint — Verify project scaffolding
  - Run `dotnet build` and confirm the new project compiles with zero errors. Ensure all tests pass, ask the user if questions arise.

- [ ] 3. Implement library source files
  - [ ] 3.1 Create `src/QuadStore.SparqlServer/SparqlResultSerializer.cs`
    - Internal static class in namespace `TripleStore.SparqlServer`
    - `SerializeResult(object result)` — dispatches on `SparqlResultSet` → `SparqlJsonWriter` (content type `application/sparql-results+json`) vs `IGraph` → `CompressingTurtleWriter` (content type `text/turtle`)
    - `SerializeGraph(IGraph graph)` — serializes an `IGraph` as Turtle for Graph Store GET
    - _Requirements: 3.3, 3.4, 9.1, 9.2, 9.3, 9.4_

  - [ ] 3.2 Create `src/QuadStore.SparqlServer/SparqlQueryHandler.cs`
    - Internal static class in namespace `TripleStore.SparqlServer`
    - `HandleGet(HttpContext)` — extracts `query` from query string, validates, executes via `IQueryableStorage` from DI, serializes result; returns 400 if missing
    - `HandlePost(HttpContext)` — inspects `Content-Type` to dispatch: `application/sparql-query` reads body as query, `application/x-www-form-urlencoded` reads `query` form field, `application/sparql-update` delegates to update logic (reads body, returns 501 via `NotImplementedException`), unsupported types return 400
    - Error handling: `RdfParseException` → 400, `NotImplementedException` → 501, `RdfStorageException` containing "not support" → 501, other → 500 with generic message
    - _Requirements: 2.3, 2.4, 3.1, 3.2, 4.1, 4.2, 4.3, 4.4, 5.1, 5.2, 9.5, 9.6, 9.7, 9.8_

  - [ ] 3.3 Create `src/QuadStore.SparqlServer/GraphStoreHandler.cs`
    - Internal static class in namespace `TripleStore.SparqlServer`
    - `HandleGet(HttpContext)` — extracts optional `graph` query parameter, resolves `IQueryableStorage` from DI, casts to `IStorageProvider`, calls `LoadGraph`, serializes as Turtle; if cast fails returns 501
    - `HandlePut(HttpContext)` — returns 501 with "Graph replacement is not supported by the backend."
    - `HandlePost(HttpContext)` — returns 501 with "Graph merging is not supported by the backend."
    - `HandleDelete(HttpContext)` — returns 501 with "Graph deletion is not supported by the backend."
    - Error handling: unexpected exceptions → 500 with generic message
    - _Requirements: 9.9, 9.10, 9.11, 9.12, 9.13, 9.14, 9.15_

  - [ ] 3.4 Create `src/QuadStore.SparqlServer/SparqlEndpointRouteBuilderExtensions.cs`
    - Public static class in namespace `TripleStore.SparqlServer`
    - `MapSparqlEndpoints(this IEndpointRouteBuilder endpoints, string routePrefix = "/sparql")` extension method
    - Registers: GET `{routePrefix}` → `SparqlQueryHandler.HandleGet`, POST `{routePrefix}` → `SparqlQueryHandler.HandlePost`
    - Registers: GET `{routePrefix}/graph` → `GraphStoreHandler.HandleGet`, PUT → `HandlePut`, POST → `HandlePost`, DELETE → `HandleDelete`
    - Returns `IEndpointRouteBuilder` for chaining
    - _Requirements: 2.1, 2.2, 9.5, 9.9_

- [ ] 4. Checkpoint — Verify library compiles
  - Run `dotnet build` and confirm the library project compiles with zero errors. Ensure all tests pass, ask the user if questions arise.

- [ ] 5. Refactor the sample app to use the library
  - [ ] 5.1 Update `samples/SparqlServerOverMinimalApi/SparqlServerOverMinimalApi.csproj`
    - Add a project reference to `..\..\src\QuadStore.SparqlServer\QuadStore.SparqlServer.csproj`
    - Keep the existing `QuadStore.Core` project reference
    - _Requirements: 6.1_

  - [ ] 5.2 Simplify `samples/SparqlServerOverMinimalApi/Program.cs`
    - Add `using TripleStore.SparqlServer;`
    - Remove Section D (GET handler), Section E (POST handler), and Section F (`SerializeResult` helper)
    - Replace with a single `app.MapSparqlEndpoints();` call
    - Retain Sections A (store init), B (seed data), C (DI registration), and G (shutdown)
    - Remove unused `using` directives (`VDS.RDF.Writing`, `VDS.RDF.Parsing`, `VDS.RDF.Query`, `System.IO`, `StringWriter` alias) that were only needed by the removed inline handlers
    - _Requirements: 6.2, 6.3, 6.4_

- [ ] 6. Checkpoint — Verify sample app builds and existing tests pass
  - Run `dotnet build` and `dotnet test` to confirm the sample app compiles and all existing tests in `SparqlServerTests.cs` and `SparqlServerPropertyTests.cs` pass unchanged. Ensure all tests pass, ask the user if questions arise.

- [ ] 7. Retarget test project and add new tests
  - [ ] 7.1 Add library project reference to `test/QuadStore.Tests/QuadStore.Tests.csproj`
    - Add `<ProjectReference Include="..\..\src\QuadStore.SparqlServer\QuadStore.SparqlServer.csproj" />`
    - Keep existing project references (Core, Storage, SparqlServerOverMinimalApi sample)
    - _Requirements: 7.1, 7.2, 7.3_

  - [ ] 7.2 Add new integration tests to `test/QuadStore.Tests/SparqlServerTests.cs`
    - SPARQL Update POST with empty body → 400
    - SPARQL Update POST with non-empty body → 501
    - Graph Store GET with named graph → 200 + `text/turtle`
    - Graph Store GET without `graph` param (default graph) → 200 + `text/turtle`
    - Graph Store PUT → 501
    - Graph Store POST → 501
    - Graph Store DELETE → 501
    - ASK query → 200 + `application/sparql-results+json`
    - DESCRIBE query → 200 + `text/turtle`
    - Do NOT modify or remove any existing tests
    - _Requirements: 9.1, 9.2, 9.3, 9.4, 9.5, 9.8, 9.10, 9.11, 9.12, 9.13, 9.14_

  - [ ]* 7.3 Write property test for SPARQL Update always returns 501
    - **Property 4: SPARQL Update always returns 501**
    - Generator: produce random non-empty strings (valid and invalid SPARQL Update syntax)
    - Oracle: POST with `application/sparql-update` always returns 501
    - Minimum 100 iterations via `QuickCheckThrowOnFailure()`
    - Add to `test/QuadStore.Tests/SparqlServerPropertyTests.cs`
    - **Validates: Requirements 9.8**

  - [ ]* 7.4 Write property test for Graph Store write operations return 501
    - **Property 5: Graph Store write operations return 501**
    - Generator: produce random graph URIs and request bodies
    - For each, submit PUT, POST, and DELETE to `/sparql/graph`
    - Oracle: all three return 501
    - Minimum 100 iterations via `QuickCheckThrowOnFailure()`
    - Add to `test/QuadStore.Tests/SparqlServerPropertyTests.cs`
    - **Validates: Requirements 9.12, 9.13, 9.14**

  - [ ]* 7.5 Write property test for Graph Store GET returns Turtle content type
    - **Property 6: Graph Store GET returns Turtle content type**
    - Generator: use known graph URIs from seed data
    - Oracle: GET `/sparql/graph?graph={uri}` returns 200 with `text/turtle`
    - Minimum 100 iterations via `QuickCheckThrowOnFailure()`
    - Add to `test/QuadStore.Tests/SparqlServerPropertyTests.cs`
    - **Validates: Requirements 9.10**

- [ ] 8. Final checkpoint — Ensure all tests pass
  - Run `dotnet test` and confirm all existing and new tests pass. Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- Existing tests in `SparqlServerTests.cs` and `SparqlServerPropertyTests.cs` must continue to pass unchanged throughout
