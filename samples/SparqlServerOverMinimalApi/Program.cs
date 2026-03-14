// ============================================================================
// SPARQL Server over ASP.NET Core Minimal API
// ============================================================================
// This sample demonstrates how to build a SPARQL-compatible HTTP query endpoint
// using .NET 10 minimal API, backed by QuadStore (columnar bitmap-indexed quad
// store) and exposed through the QuadStore.SparqlServer library.
//
// Sections:
//   A — Store initialization
//   B — Seed data loading
//   C — Host configuration and DI registration
//   G — Graceful shutdown and app.Run()
// ============================================================================

using TripleStore.Core;
using TripleStore.SparqlServer;
using VDS.RDF;
using VDS.RDF.Storage;

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

app.MapSparqlEndpoints();

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
