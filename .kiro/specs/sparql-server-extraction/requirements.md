# Requirements Document

## Introduction

Extract the reusable SPARQL HTTP server logic from the sample at `samples/SparqlServerOverMinimalApi/` into a new library project `src/QuadStore.SparqlServer/`. The new project exposes extension methods that allow any ASP.NET Core minimal API application to add SPARQL Protocol GET and POST endpoints backed by a dotNetRDF `IQueryableStorage`. The sample is then simplified to consume the new library, and the existing integration tests and property-based tests are retargeted to test the library directly.

## Glossary

- **SparqlServer_Library**: The new class library project at `src/QuadStore.SparqlServer/` that provides reusable SPARQL endpoint registration for ASP.NET Core minimal API applications.
- **Extension_Methods**: Static methods on `IEndpointRouteBuilder` (or `WebApplication`) that register the SPARQL GET and POST endpoints and the result serialization logic.
- **Sample_App**: The existing sample application at `samples/SparqlServerOverMinimalApi/` that demonstrates using the SparqlServer_Library with a QuadStore backend.
- **SPARQL_Query_Endpoint**: An HTTP endpoint that accepts SPARQL queries via GET or POST and returns results in standard W3C formats.
- **IQueryableStorage**: The dotNetRDF interface that provides `Query(string sparqlQuery)` for executing SPARQL queries against a storage backend.
- **Solution_File**: The `QuadStore.sln` file at the repository root that tracks all projects.
- **Test_Project**: The existing xUnit test project at `test/QuadStore.Tests/`.
- **SPARQL_Update_Endpoint**: An HTTP endpoint that accepts SPARQL Update requests via POST with content type `application/sparql-update`, per SPARQL 1.1 Protocol §2.2.
- **Graph_Store_Endpoint**: An HTTP endpoint that supports the SPARQL 1.1 Graph Store HTTP Protocol for managing named graphs via GET, PUT, POST, and DELETE on a graph store path (e.g., `/sparql/graph`).
- **NotImplementedException**: A .NET `System.NotImplementedException` thrown to indicate that an endpoint exists but the underlying QuadStore does not yet support the requested operation.

## Requirements

### Requirement 1: New Library Project Structure

**User Story:** As a developer, I want a reusable library project for the SPARQL server logic, so that I can add SPARQL endpoints to any ASP.NET Core application without copying code from the sample.

#### Acceptance Criteria

1. THE SparqlServer_Library SHALL be a class library project at `src/QuadStore.SparqlServer/QuadStore.SparqlServer.csproj`.
2. THE SparqlServer_Library SHALL use the `Microsoft.NET.Sdk` SDK and target `net10.0`.
3. THE SparqlServer_Library SHALL reference `QuadStore.Core` as a project reference.
4. THE SparqlServer_Library SHALL reference `Microsoft.AspNetCore.App` as a framework reference to access ASP.NET Core types without requiring the Web SDK.
5. THE SparqlServer_Library SHALL use the namespace `TripleStore.SparqlServer` consistent with the existing namespace convention (`TripleStore.Core`).
6. THE Solution_File SHALL include the SparqlServer_Library project nested under the `Src` solution folder.

### Requirement 2: SPARQL Endpoint Registration via Extension Methods

**User Story:** As a developer, I want to call a single extension method to register SPARQL endpoints on my ASP.NET Core application, so that setup is minimal and discoverable.

#### Acceptance Criteria

1. THE SparqlServer_Library SHALL provide an extension method `MapSparqlEndpoints` on `IEndpointRouteBuilder` that registers both GET and POST handlers at a caller-specified route path.
2. WHEN `MapSparqlEndpoints` is called without a route path argument, THE SparqlServer_Library SHALL default the route path to `"/sparql"`.
3. WHEN `MapSparqlEndpoints` is called, THE SparqlServer_Library SHALL resolve `IQueryableStorage` from the dependency injection container to execute queries.
4. THE SparqlServer_Library SHALL not depend on `QuadStore` directly for endpoint logic; the Extension_Methods SHALL operate against the `IQueryableStorage` abstraction only.

### Requirement 3: SPARQL Protocol GET Binding

**User Story:** As a SPARQL client, I want to submit queries via HTTP GET with a `query` parameter, so that I can use the standard SPARQL Protocol GET binding.

#### Acceptance Criteria

1. WHEN an HTTP GET request is received at the configured route with a `query` query-string parameter, THE SPARQL_Query_Endpoint SHALL execute the SPARQL query against the IQueryableStorage resolved from DI.
2. WHEN the `query` parameter is missing from an HTTP GET request, THE SPARQL_Query_Endpoint SHALL return HTTP 400 with a descriptive error message.
3. WHEN the SPARQL query produces a `SparqlResultSet` (SELECT/ASK), THE SPARQL_Query_Endpoint SHALL serialize the results as SPARQL Results JSON with content type `application/sparql-results+json`.
4. WHEN the SPARQL query produces an `IGraph` (CONSTRUCT/DESCRIBE), THE SPARQL_Query_Endpoint SHALL serialize the results as Turtle with content type `text/turtle`.

### Requirement 4: SPARQL Protocol POST Bindings

**User Story:** As a SPARQL client, I want to submit queries via HTTP POST, so that I can send large queries that exceed URL length limits.

#### Acceptance Criteria

1. WHEN an HTTP POST request is received with content type `application/sparql-query`, THE SPARQL_Query_Endpoint SHALL read the request body as the SPARQL query string and execute it.
2. WHEN an HTTP POST request is received with content type `application/x-www-form-urlencoded` containing a `query` field, THE SPARQL_Query_Endpoint SHALL extract the query from the form body and execute it.
3. WHEN the POST request body is empty or the `query` field is missing, THE SPARQL_Query_Endpoint SHALL return HTTP 400 with a descriptive error message.
4. WHEN the POST request has an unsupported content type, THE SPARQL_Query_Endpoint SHALL return HTTP 400 with a descriptive error message.

### Requirement 5: Error Handling

**User Story:** As a SPARQL client, I want to receive clear error responses when my query is malformed or an internal error occurs, so that I can diagnose issues.

#### Acceptance Criteria

1. IF a SPARQL query fails to parse (throws `RdfParseException`), THEN THE SPARQL_Query_Endpoint SHALL return HTTP 400 with the parse error message in the response body.
2. IF an unexpected error occurs during query execution, THEN THE SPARQL_Query_Endpoint SHALL return HTTP 500 with a generic error message that does not expose internal stack traces.

### Requirement 6: Sample App Simplification

**User Story:** As a developer reading the sample, I want the sample to demonstrate how to use the SparqlServer_Library, so that I can see the minimal code needed to stand up a SPARQL endpoint.

#### Acceptance Criteria

1. THE Sample_App SHALL reference the SparqlServer_Library project instead of containing inline SPARQL endpoint logic.
2. THE Sample_App SHALL call `MapSparqlEndpoints` to register the SPARQL endpoints.
3. THE Sample_App SHALL retain its own store initialization, seed data loading, DI registration of `QuadStore`, `QuadStoreStorageProvider`, and `IQueryableStorage`, and graceful shutdown logic.
4. THE Sample_App SHALL remove the inline GET handler, POST handler, and `SerializeResult` helper that are now provided by the SparqlServer_Library.

### Requirement 7: Test Retargeting

**User Story:** As a developer, I want the existing SPARQL server tests to validate the new library project, so that the extracted code is covered by the same test suite.

#### Acceptance Criteria

1. THE Test_Project SHALL reference the SparqlServer_Library project.
2. THE Test_Project SHALL continue to use `WebApplicationFactory<Program>` from the Sample_App to spin up an in-memory test server, since the library requires a host application to run.
3. THE Test_Project SHALL retain all existing integration tests in `SparqlServerTests.cs` and property-based tests in `SparqlServerPropertyTests.cs` with identical test logic and assertions.
4. WHEN all tests pass against the refactored sample using the SparqlServer_Library, THE Test_Project SHALL confirm that the extraction preserved the original behavior.

### Requirement 8: Solution File Update

**User Story:** As a developer, I want the solution file to include the new project, so that `dotnet build` and IDE tooling discover it automatically.

#### Acceptance Criteria

1. THE Solution_File SHALL contain a project entry for `QuadStore.SparqlServer` with a unique project GUID.
2. THE Solution_File SHALL nest the `QuadStore.SparqlServer` project under the existing `Src` solution folder.
3. THE Solution_File SHALL include Debug and Release build configurations for the new project.

### Requirement 9: SPARQL Query Forms, Update Endpoint, and Graph Store Protocol

**User Story:** As a SPARQL client, I want the server to expose endpoints for all SPARQL query forms, SPARQL Update, and Graph Store management, so that the API surface is complete per the W3C specifications even when the backend does not yet support all operations.

#### Acceptance Criteria

##### Query Forms (covered by existing endpoints)

1. WHEN a SELECT query is submitted to the SPARQL_Query_Endpoint, THE SparqlServer_Library SHALL execute the query and return a `SparqlResultSet` serialized as SPARQL Results JSON.
2. WHEN a CONSTRUCT query is submitted to the SPARQL_Query_Endpoint, THE SparqlServer_Library SHALL execute the query and return an `IGraph` serialized as Turtle.
3. WHEN a DESCRIBE query is submitted to the SPARQL_Query_Endpoint, THE SparqlServer_Library SHALL execute the query and return an `IGraph` serialized as Turtle.
4. WHEN an ASK query is submitted to the SPARQL_Query_Endpoint, THE SparqlServer_Library SHALL execute the query and return a `SparqlResultSet` serialized as SPARQL Results JSON.

##### SPARQL Update Endpoint

5. WHEN `MapSparqlEndpoints` is called, THE SparqlServer_Library SHALL register a POST handler at the configured route path that accepts content type `application/sparql-update` for SPARQL Update requests.
6. WHEN an HTTP POST request with content type `application/sparql-update` is received, THE SPARQL_Update_Endpoint SHALL read the request body as the SPARQL Update string.
7. WHEN the SPARQL Update request body is empty, THE SPARQL_Update_Endpoint SHALL return HTTP 400 with a descriptive error message.
8. WHILE the underlying IQueryableStorage does not support SPARQL Update (i.e., the storage provider throws `RdfStorageException` on `Update()`), THE SPARQL_Update_Endpoint SHALL return HTTP 501 and throw a `NotImplementedException` indicating that SPARQL Update is not yet supported by the backend.

##### Graph Store Protocol Endpoints

9. WHEN `MapSparqlEndpoints` is called, THE SparqlServer_Library SHALL register Graph_Store_Endpoint handlers for GET, PUT, POST, and DELETE at a graph store sub-path (defaulting to `"/sparql/graph"`).
10. WHEN an HTTP GET request is received at the Graph_Store_Endpoint with a `graph` query-string parameter identifying a named graph, THE SparqlServer_Library SHALL retrieve the graph from the IQueryableStorage and return it serialized as Turtle with content type `text/turtle`.
11. WHEN an HTTP GET request is received at the Graph_Store_Endpoint without a `graph` parameter, THE SparqlServer_Library SHALL return the default graph serialized as Turtle.
12. WHILE the underlying IQueryableStorage does not support graph replacement, THE Graph_Store_Endpoint SHALL return HTTP 501 and throw a `NotImplementedException` for PUT requests.
13. WHILE the underlying IQueryableStorage does not support graph merging via the Graph Store Protocol, THE Graph_Store_Endpoint SHALL return HTTP 501 and throw a `NotImplementedException` for POST requests.
14. WHILE the underlying IQueryableStorage does not support graph deletion (i.e., `DeleteSupported` is false), THE Graph_Store_Endpoint SHALL return HTTP 501 and throw a `NotImplementedException` for DELETE requests.
15. IF an unexpected error occurs during any Graph_Store_Endpoint operation, THEN THE Graph_Store_Endpoint SHALL return HTTP 500 with a generic error message that does not expose internal stack traces.
