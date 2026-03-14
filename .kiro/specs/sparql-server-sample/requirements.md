# Requirements Document

## Introduction

Transform the boilerplate console app at `samples/SparqlServerOverMinimalApi` into a working sample that demonstrates how to build a SPARQL-compatible HTTP server endpoint using .NET 10 minimal API and dotNetRDF. The backing store is a `QuadStore` persisting to disk via memory-mapped files, exposed through `QuadStoreStorageProvider`. The sample should be small, self-contained, and thoroughly documented with XML doc comments explaining each step.

## Glossary

- **Sample_App**: The .NET 10 minimal API application located at `samples/SparqlServerOverMinimalApi` that hosts the SPARQL endpoint.
- **QuadStore**: The columnar, bitmap-indexed quad store from `TripleStore.Core` that persists RDF quads to disk using memory-mapped files.
- **Storage_Provider**: The `QuadStoreStorageProvider` adapter that wraps a `QuadStore` and implements dotNetRDF's `IQueryableStorage` interface, enabling SPARQL query execution via the Leviathan engine.
- **SPARQL_Query_Endpoint**: An HTTP endpoint that accepts SPARQL queries and returns results in standard W3C formats.
- **SPARQL_Protocol**: The W3C SPARQL 1.1 Protocol specification defining how SPARQL queries are submitted over HTTP (GET with `?query=` parameter, POST with `application/sparql-query` content type, or POST with `application/x-www-form-urlencoded` body containing a `query` field).
- **Results_Serializer**: A dotNetRDF writer that serializes `SparqlResultSet` or `IGraph` objects into standard response formats (SPARQL Results XML, SPARQL Results JSON, Turtle, RDF/XML).

## Requirements

### Requirement 1: Project Configuration

**User Story:** As a developer, I want the sample project to reference QuadStore.Core and the ASP.NET Core minimal API SDK, so that I can build and run the SPARQL server with a single `dotnet run`.

#### Acceptance Criteria

1. THE Sample_App SHALL use the `Microsoft.NET.Sdk.Web` SDK and target `net10.0`.
2. THE Sample_App SHALL include a `ProjectReference` to `QuadStore.Core`.
3. THE Sample_App SHALL not require any additional NuGet packages beyond those transitively provided by QuadStore.Core and the Web SDK.

### Requirement 2: QuadStore Lifecycle Management

**User Story:** As a developer, I want the QuadStore to be created at startup and disposed at shutdown, so that the memory-mapped files are managed correctly.

#### Acceptance Criteria

1. WHEN the Sample_App starts, THE Sample_App SHALL create a QuadStore instance rooted at a configurable data directory path.
2. WHEN the Sample_App starts, THE Sample_App SHALL create a Storage_Provider wrapping the QuadStore instance.
3. WHEN the Sample_App shuts down, THE Sample_App SHALL dispose the QuadStore instance to release memory-mapped file handles.
4. THE Sample_App SHALL register the QuadStore and Storage_Provider as singleton services available to the request pipeline.

### Requirement 3: Seed Data Loading

**User Story:** As a developer exploring the sample, I want the store to contain sample RDF data on first run, so that I can immediately test queries without manual data loading.

#### Acceptance Criteria

1. WHEN the Sample_App starts and the QuadStore data directory is empty, THE Sample_App SHALL load a small embedded set of sample RDF triples into the QuadStore.
2. WHEN the Sample_App starts and the QuadStore data directory already contains persisted data, THE Sample_App SHALL skip seed data loading and use the existing data.
3. THE Sample_App SHALL persist the seeded data to disk so that subsequent runs reuse the same data.

### Requirement 4: SPARQL Query via HTTP GET

**User Story:** As a SPARQL client, I want to submit queries via HTTP GET with a `query` parameter, so that I can use the standard SPARQL Protocol GET binding.

#### Acceptance Criteria

1. WHEN an HTTP GET request is received at `/sparql` with a `query` query-string parameter, THE SPARQL_Query_Endpoint SHALL execute the SPARQL query against the Storage_Provider.
2. WHEN the `query` parameter is missing from an HTTP GET request to `/sparql`, THE SPARQL_Query_Endpoint SHALL return HTTP 400 with a descriptive error message.
3. WHEN the SPARQL query executes successfully and produces a `SparqlResultSet`, THE SPARQL_Query_Endpoint SHALL serialize the results as SPARQL Results JSON with content type `application/sparql-results+json`.
4. WHEN the SPARQL query executes successfully and produces an `IGraph` (CONSTRUCT/DESCRIBE), THE SPARQL_Query_Endpoint SHALL serialize the results as Turtle with content type `text/turtle`.

### Requirement 5: SPARQL Query via HTTP POST

**User Story:** As a SPARQL client, I want to submit queries via HTTP POST, so that I can send large queries that exceed URL length limits.

#### Acceptance Criteria

1. WHEN an HTTP POST request is received at `/sparql` with content type `application/sparql-query`, THE SPARQL_Query_Endpoint SHALL read the request body as the SPARQL query string and execute it against the Storage_Provider.
2. WHEN an HTTP POST request is received at `/sparql` with content type `application/x-www-form-urlencoded` containing a `query` field, THE SPARQL_Query_Endpoint SHALL extract the query from the form body and execute it against the Storage_Provider.
3. WHEN the POST request body is empty or the `query` field is missing, THE SPARQL_Query_Endpoint SHALL return HTTP 400 with a descriptive error message.

### Requirement 6: SPARQL Query Error Handling

**User Story:** As a SPARQL client, I want to receive clear error responses when my query is malformed, so that I can diagnose and fix issues.

#### Acceptance Criteria

1. IF a SPARQL query fails to parse, THEN THE SPARQL_Query_Endpoint SHALL return HTTP 400 with the parse error message in the response body.
2. IF an unexpected error occurs during query execution, THEN THE SPARQL_Query_Endpoint SHALL return HTTP 500 with a generic error message that does not expose internal stack traces.

### Requirement 7: Documentation via Code Comments

**User Story:** As a developer reading the sample, I want every significant code block to have an explanatory XML doc comment or inline comment, so that I can understand the purpose and mechanics of each step.

#### Acceptance Criteria

1. THE Sample_App SHALL include an XML doc comment or inline comment for each logical section of `Program.cs` explaining its purpose.
2. THE Sample_App SHALL include comments that reference the relevant SPARQL Protocol concepts (GET binding, POST direct, POST URL-encoded).
3. THE Sample_App SHALL include a comment at the top of `Program.cs` summarizing what the sample demonstrates.
