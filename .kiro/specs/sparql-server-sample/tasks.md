# Implementation Plan: SPARQL Server over Minimal API

## Overview

Transform the boilerplate console app at `samples/SparqlServerOverMinimalApi` into a working ASP.NET Core minimal API sample that exposes a SPARQL query endpoint backed by QuadStore. Implementation is a single-file `Program.cs` plus `.csproj` changes, with integration tests using `WebApplicationFactory<Program>` and property-based tests using FsCheck.Xunit.

## Tasks

- [x] 1. Configure project file and test infrastructure
  - [x] 1.1 Update `samples/SparqlServerOverMinimalApi/SparqlServerOverMinimalApi.csproj`
    - Change SDK from `Microsoft.NET.Sdk` to `Microsoft.NET.Sdk.Web`
    - Remove `<OutputType>Exe</OutputType>`
    - Add `<ProjectReference Include="..\..\src\QuadStore.Core\QuadStore.Core.csproj" />`
    - Keep `<TargetFramework>net10.0</TargetFramework>`, `<ImplicitUsings>enable</ImplicitUsings>`, `<Nullable>enable</Nullable>`
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Update `test/QuadStore.Tests/QuadStore.Tests.csproj` for integration testing
    - Add `<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" />` for `WebApplicationFactory<Program>`
    - Add `<PackageReference Include="FsCheck.Xunit" />` for property-based tests
    - Add `<ProjectReference>` to `samples/SparqlServerOverMinimalApi/SparqlServerOverMinimalApi.csproj`
    - _Requirements: (test infrastructure)_

- [x] 2. Implement Program.cs — store initialization, seed data, and host configuration
  - [x] 2.1 Implement store initialization and seed data loading (Sections A–C)
    - Replace boilerplate `Console.WriteLine` with top-level statements
    - Add a summary comment at the top of `Program.cs` explaining what the sample demonstrates
    - Create `QuadStore` instance rooted at a configurable data directory (default `./quadstore-data`)
    - Wrap in `QuadStoreStorageProvider`, cast to `IQueryableStorage`
    - Check if store is empty via `store.Query().Any()`; if empty, load embedded TriG seed data via `SinglePassTrigLoader.LoadFromString()` and call `store.SaveAll()`
    - Register `QuadStore` and `QuadStoreStorageProvider` as singletons via `builder.Services.AddSingleton`
    - Add XML doc comments / inline comments for each logical section
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 3.1, 3.2, 3.3, 7.1_

- [x] 3. Implement SPARQL endpoints and serialization
  - [x] 3.1 Implement GET /sparql endpoint (Section D)
    - Map `GET /sparql` that extracts `query` from query string
    - Return 400 if `query` parameter is missing with message "Missing required 'query' parameter."
    - Execute query via `IQueryableStorage.Query(query)`
    - Serialize result using the `SerializeResult` helper (task 3.3)
    - Catch `RdfParseException` → 400 with parse error message
    - Catch other exceptions → 500 with generic message "An internal error occurred while processing the query."
    - Add inline comments referencing SPARQL Protocol GET binding
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 6.1, 6.2, 7.2_

  - [x] 3.2 Implement POST /sparql endpoint (Section E)
    - Map `POST /sparql` that checks `Content-Type`:
      - `application/sparql-query`: read body as query string; return 400 if empty with "Request body is empty."
      - `application/x-www-form-urlencoded`: extract `query` from form; return 400 if missing with "Missing required 'query' form field."
      - Other content types: return 400 with "Unsupported Content-Type. Use 'application/sparql-query' or 'application/x-www-form-urlencoded'."
    - Execute and serialize identically to GET handler
    - Same error handling as GET
    - Add inline comments referencing SPARQL Protocol POST bindings
    - _Requirements: 5.1, 5.2, 5.3, 6.1, 6.2, 7.2_

  - [x] 3.3 Implement SerializeResult helper and shutdown (Sections F–G)
    - Create local function `SerializeResult(object result)` that:
      - If `SparqlResultSet`: serialize via `SparqlResultsJsonWriter` to `StringWriter`, return `Results.Content(json, "application/sparql-results+json")`
      - If `IGraph`: serialize via `CompressingTurtleWriter` to `StringWriter`, return `Results.Content(turtle, "text/turtle")`
    - Register `app.Lifetime.ApplicationStopping` callback to call `qs.Dispose()`
    - Call `app.Run()`
    - _Requirements: 4.3, 4.4, 2.3, 7.1_

  - [x] 3.4 Add a `public partial class Program { }` declaration at the end of Program.cs
    - Required for `WebApplicationFactory<Program>` to discover the entry point from the test project
    - _Requirements: (test infrastructure)_

- [x] 4. Checkpoint — Verify the sample builds and runs
  - Ensure `dotnet build` succeeds for both the sample project and the test project. Ask the user if questions arise.

- [x] 5. Write unit tests for SPARQL server endpoints
  - [x] 5.1 Create `test/QuadStore.Tests/SparqlServerTests.cs` with integration tests using `WebApplicationFactory<Program>`
    - Each test should use an isolated temp directory for QuadStore data to avoid cross-test interference
    - Implement a custom `WebApplicationFactory` subclass or use `WithWebHostBuilder` to override the data directory
    - _Requirements: (test infrastructure)_

  - [x] 5.2 Write unit test: GET /sparql without query parameter returns 400
    - `GET /sparql` → assert HTTP 400, response body contains "Missing required 'query' parameter."
    - _Requirements: 4.2_

  - [x] 5.3 Write unit test: POST /sparql with empty body returns 400
    - `POST /sparql` with `Content-Type: application/sparql-query` and empty body → assert HTTP 400
    - _Requirements: 5.3_

  - [x] 5.4 Write unit test: POST /sparql with missing form field returns 400
    - `POST /sparql` with `Content-Type: application/x-www-form-urlencoded` without `query` field → assert HTTP 400
    - _Requirements: 5.3_

  - [x] 5.5 Write unit test: POST /sparql with unsupported content type returns 400
    - `POST /sparql` with `Content-Type: text/plain` → assert HTTP 400, body contains "Unsupported Content-Type"
    - _Requirements: 5.3_

  - [x] 5.6 Write unit test: Successful SELECT query returns 200 with correct content type
    - `GET /sparql?query=SELECT ?s ?p ?o WHERE { ?s ?p ?o } LIMIT 1` → assert HTTP 200, content type `application/sparql-results+json`, valid JSON body
    - _Requirements: 4.1, 4.3_

  - [x] 5.7 Write unit test: Successful CONSTRUCT query returns 200 with Turtle content type
    - `GET /sparql?query=CONSTRUCT { ?s ?p ?o } WHERE { ?s ?p ?o } LIMIT 1` → assert HTTP 200, content type `text/turtle`
    - _Requirements: 4.1, 4.4_

  - [x] 5.8 Write unit test: Malformed SPARQL query returns 400
    - `GET /sparql?query=SELCT * WHERE { ?s ?p ?o }` → assert HTTP 400, non-empty body
    - _Requirements: 6.1_

  - [x] 5.9 Write unit test: Internal error response does not expose stack traces
    - Verify 500 response body equals "An internal error occurred while processing the query." and does not contain "at " or "Exception"
    - _Requirements: 6.2_

  - [x] 5.10 Write unit test: Seed data is loaded on first run
    - Start app with empty data directory → `GET /sparql?query=SELECT ?s WHERE { ?s ?p ?o } LIMIT 1` → assert 200 with non-empty results
    - _Requirements: 3.1, 3.3_

- [x] 6. Checkpoint — Ensure all unit tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Write property-based tests for correctness properties
  - [x] 7.1 Write property test for Property 1: Response content-type matches query result type
    - **Property 1: Response content-type matches query result type**
    - **Validates: Requirements 4.3, 4.4**
    - Create `test/QuadStore.Tests/SparqlServerPropertyTests.cs`
    - Use FsCheck.Xunit `[Property]` attribute with `MaxTest = 100`
    - Generator: produce random valid SELECT and CONSTRUCT queries against the seed data schema (varying variable names, WHERE patterns using `foaf:name`, `foaf:knows`, `ex:alice`, `ex:bob`)
    - Oracle: SELECT/ASK queries → assert content type is `application/sparql-results+json`; CONSTRUCT/DESCRIBE queries → assert content type is `text/turtle`

  - [x] 7.2 Write property test for Property 2: Protocol equivalence across submission methods
    - **Property 2: Protocol equivalence across submission methods**
    - **Validates: Requirements 4.1, 5.1, 5.2**
    - Use FsCheck.Xunit `[Property]` attribute with `MaxTest = 100`
    - Generator: produce random valid SPARQL queries
    - For each query, submit via GET (`?query=`), POST `application/sparql-query`, and POST `application/x-www-form-urlencoded`
    - Oracle: assert all three responses have identical status codes, content types, and response bodies

  - [x] 7.3 Write property test for Property 3: Malformed SPARQL queries return 400
    - **Property 3: Malformed SPARQL queries return 400**
    - **Validates: Requirements 6.1**
    - Use FsCheck.Xunit `[Property]` attribute with `MaxTest = 100`
    - Generator: produce random strings that are not valid SPARQL (random alphanumeric, partial keywords, SQL statements)
    - Oracle: assert HTTP 400 status and non-empty response body

- [x] 8. Final checkpoint — Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties from the design document
- Unit tests validate specific examples and edge cases
- All tests use `WebApplicationFactory<Program>` with isolated temp directories for QuadStore data
- Code style: 4-space indentation, Allman braces, `var` preferred, nullable enabled
