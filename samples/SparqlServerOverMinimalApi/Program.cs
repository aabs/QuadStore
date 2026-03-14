// ============================================================================
// SPARQL Server over ASP.NET Core Minimal API
// ============================================================================
// This sample demonstrates how to build a SPARQL-compatible HTTP query endpoint
// using .NET 10 minimal API, backed by QuadStore (columnar bitmap-indexed quad
// store) and exposed through QuadStoreStorageProvider (dotNetRDF adapter).
//
// Sections:
//   A — Store initialization
//   B — Seed data loading
//   C — Host configuration and DI registration
//   D — GET /sparql endpoint (SPARQL Protocol GET binding)
//   E — POST /sparql endpoint (SPARQL Protocol POST bindings)
//   F — SerializeResult helper
//   G — Graceful shutdown and app.Run()
// ============================================================================

using System.IO;
using TripleStore.Core;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Storage;
using VDS.RDF.Writing;

using StringWriter = System.IO.StringWriter;

// ---------------------------------------------------------------------------
// Section A: Store Initialization
// ---------------------------------------------------------------------------
// Create a QuadStore rooted at a configurable data directory. The store uses
// memory-mapped columnar files with roaring bitmap indexes for efficient
// append and query operations.

var dataDir = args.Length > 0 ? args[0] : "./quadstore-data";
var qs = new QuadStore(dataDir);
var provider = new QuadStoreStorageProvider(qs);
var queryable = (IQueryableStorage)provider;

// ---------------------------------------------------------------------------
// Section B: Seed Data
// ---------------------------------------------------------------------------
// On first run (empty store), load a small set of FOAF triples so the sample
// can be queried immediately without manual data loading.

if (!qs.Query().Any())
{
    var seedTriG = """
        @prefix foaf: <http://xmlns.com/foaf/0.1/> .
        @prefix ex:   <http://example.org/> .

        {
          ex:alice foaf:name "Alice" ;
                   foaf:knows ex:bob .
          ex:bob   foaf:name "Bob" ;
                   foaf:knows ex:alice .
        }
        """;

    var loader = new SinglePassTrigLoader(qs);
    loader.LoadFromString(seedTriG);
    qs.SaveAll();
}

// ---------------------------------------------------------------------------
// Section C: Host Configuration
// ---------------------------------------------------------------------------
// Build the ASP.NET Core minimal API host and register QuadStore and its
// storage provider as singletons so they are available to the request pipeline.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(qs);
builder.Services.AddSingleton(provider);
builder.Services.AddSingleton<IQueryableStorage>(provider);

var app = builder.Build();

// ---------------------------------------------------------------------------
// Section D: GET /sparql endpoint
// ---------------------------------------------------------------------------
// SPARQL Protocol §2.1.1 — Query via GET
// The client sends the query as a URL-encoded query-string parameter named
// "query". This is the simplest binding and works for short queries.

app.MapGet("/sparql", (HttpContext context) =>
{
    // Extract the SPARQL query from the ?query= parameter
    var query = context.Request.Query["query"].FirstOrDefault();

    if (string.IsNullOrWhiteSpace(query))
    {
        return Results.Text("Missing required 'query' parameter.", statusCode: 400);
    }

    try
    {
        // Execute the SPARQL query via the Leviathan engine
        var result = queryable.Query(query);
        return SerializeResult(result);
    }
    catch (Exception ex) when (ex is RdfParseException || ex.InnerException is RdfParseException)
    {
        // SPARQL parse error — return the parser message as a 400
        var parseEx = ex as RdfParseException ?? ex.InnerException as RdfParseException;
        return Results.Text(parseEx!.Message, statusCode: 400);
    }
    catch (Exception)
    {
        // Unexpected error — return a generic 500 without exposing internals
        return Results.Text(
            "An internal error occurred while processing the query.",
            statusCode: 500);
    }
});

// ---------------------------------------------------------------------------
// Section E: POST /sparql endpoint
// ---------------------------------------------------------------------------
// SPARQL Protocol §2.1.2 — Query via POST (direct) and §2.1.3 — Query via
// POST with URL-encoded parameters. Supports two content types:
//   • application/sparql-query       — body IS the SPARQL query
//   • application/x-www-form-urlencoded — body contains query= form field

app.MapPost("/sparql", async (HttpContext context) =>
{
    var contentType = context.Request.ContentType ?? string.Empty;
    string? query = null;

    if (contentType.StartsWith("application/sparql-query", StringComparison.OrdinalIgnoreCase))
    {
        // SPARQL Protocol POST direct — the request body is the query string
        using var reader = new StreamReader(context.Request.Body);
        query = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.Text("Request body is empty.", statusCode: 400);
        }
    }
    else if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
    {
        // SPARQL Protocol POST URL-encoded — extract the "query" form field
        var form = await context.Request.ReadFormAsync();
        query = form["query"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Results.Text("Missing required 'query' form field.", statusCode: 400);
        }
    }
    else
    {
        // Unsupported content type
        return Results.Text(
            "Unsupported Content-Type. Use 'application/sparql-query' or 'application/x-www-form-urlencoded'.",
            statusCode: 400);
    }

    try
    {
        // Execute the SPARQL query via the Leviathan engine
        var result = queryable.Query(query);
        return SerializeResult(result);
    }
    catch (Exception ex) when (ex is RdfParseException || ex.InnerException is RdfParseException)
    {
        // SPARQL parse error — return the parser message as a 400
        var parseEx = ex as RdfParseException ?? ex.InnerException as RdfParseException;
        return Results.Text(parseEx!.Message, statusCode: 400);
    }
    catch (Exception)
    {
        // Unexpected error — return a generic 500 without exposing internals
        return Results.Text(
            "An internal error occurred while processing the query.",
            statusCode: 500);
    }
});

// ---------------------------------------------------------------------------
// Section F: SerializeResult helper
// ---------------------------------------------------------------------------
// Serializes a dotNetRDF query result to the appropriate W3C response format.
// SELECT/ASK queries produce a SparqlResultSet → SPARQL Results JSON.
// CONSTRUCT/DESCRIBE queries produce an IGraph → Turtle.

IResult SerializeResult(object result)
{
    if (result is SparqlResultSet resultSet)
    {
        // Serialize SELECT/ASK results as SPARQL Results JSON
        var writer = new SparqlJsonWriter();
        using var sw = new StringWriter();
        ((ISparqlResultsWriter)writer).Save(resultSet, sw);
        return Results.Content(sw.ToString(), "application/sparql-results+json");
    }

    if (result is IGraph graph)
    {
        // Serialize CONSTRUCT/DESCRIBE results as Turtle
        var writer = new CompressingTurtleWriter();
        using var sw = new StringWriter();
        ((IRdfWriter)writer).Save(graph, sw);
        return Results.Content(sw.ToString(), "text/turtle");
    }

    // Fallback — should not happen with well-formed SPARQL queries
    return Results.Text(result?.ToString() ?? string.Empty, statusCode: 200);
}

// ---------------------------------------------------------------------------
// Section G: Shutdown
// ---------------------------------------------------------------------------
// Dispose the QuadStore when the application is stopping to release
// memory-mapped file handles cleanly.

app.Lifetime.ApplicationStopping.Register(() =>
{
    qs.Dispose();
});

app.Run();

// ---------------------------------------------------------------------------
// Partial class declaration required for WebApplicationFactory<Program>
// to discover the entry point from the test project.
// ---------------------------------------------------------------------------
public partial class Program { }
