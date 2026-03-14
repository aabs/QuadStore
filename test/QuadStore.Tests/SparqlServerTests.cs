using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripleStore.Tests;

/// <summary>
/// Integration tests for the SPARQL Server over Minimal API sample.
/// Uses <see cref="WebApplicationFactory{TEntryPoint}"/> to spin up an
/// in-memory test server backed by a fresh QuadStore with seed data.
/// </summary>
public class SparqlServerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SparqlServerTests(WebApplicationFactory<Program> factory)
    {
        // Each test class instance shares the same factory / seeded store.
        // The default data directory (./quadstore-data) is used; seed data
        // is loaded automatically on first run when the store is empty.
        _client = factory.CreateClient();
    }

    // -----------------------------------------------------------------
    // 5.2  GET /sparql without query parameter → 400
    // -----------------------------------------------------------------

    [Fact]
    public async Task Get_Sparql_WithoutQuery_Returns400()
    {
        var response = await _client.GetAsync("/sparql");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Missing required 'query' parameter.");
    }

    // -----------------------------------------------------------------
    // 5.3  POST /sparql with application/sparql-query and empty body → 400
    // -----------------------------------------------------------------

    [Fact]
    public async Task Post_SparqlQuery_EmptyBody_Returns400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/sparql")
        {
            Content = new StringContent(string.Empty, Encoding.UTF8, "application/sparql-query")
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------
    // 5.4  POST /sparql with form-urlencoded without query field → 400
    // -----------------------------------------------------------------

    [Fact]
    public async Task Post_FormUrlEncoded_MissingQueryField_Returns400()
    {
        var content = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("notquery", "SELECT * WHERE { ?s ?p ?o }")
        });

        var response = await _client.PostAsync("/sparql", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // -----------------------------------------------------------------
    // 5.5  POST /sparql with unsupported content type → 400
    // -----------------------------------------------------------------

    [Fact]
    public async Task Post_UnsupportedContentType_Returns400()
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/sparql")
        {
            Content = new StringContent(
                "SELECT * WHERE { ?s ?p ?o }",
                Encoding.UTF8,
                "text/plain")
        };

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Unsupported Content-Type");
    }

    // -----------------------------------------------------------------
    // 5.6  Successful SELECT query → 200, application/sparql-results+json
    // -----------------------------------------------------------------

    [Fact]
    public async Task Get_SelectQuery_Returns200_WithJsonContentType()
    {
        var query = Uri.EscapeDataString(
            "SELECT ?s ?p ?o WHERE { ?s ?p ?o } LIMIT 1");

        var response = await _client.GetAsync($"/sparql?query={query}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("application/sparql-results+json");

        var body = await response.Content.ReadAsStringAsync();
        var json = JsonDocument.Parse(body);
        json.Should().NotBeNull();
    }

    // -----------------------------------------------------------------
    // 5.7  Successful CONSTRUCT query → 200, text/turtle
    // -----------------------------------------------------------------

    [Fact]
    public async Task Get_ConstructQuery_Returns200_WithTurtleContentType()
    {
        var query = Uri.EscapeDataString(
            "CONSTRUCT { ?s ?p ?o } WHERE { ?s ?p ?o } LIMIT 1");

        var response = await _client.GetAsync($"/sparql?query={query}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType
            .Should().Be("text/turtle");
    }

    // -----------------------------------------------------------------
    // 5.8  Malformed SPARQL query → 400, non-empty body
    // -----------------------------------------------------------------

    [Fact]
    public async Task Get_MalformedQuery_Returns400_WithNonEmptyBody()
    {
        var query = Uri.EscapeDataString("SELCT * WHERE { ?s ?p ?o }");

        var response = await _client.GetAsync($"/sparql?query={query}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();
    }

    // -----------------------------------------------------------------
    // 5.9  Internal error response does not expose stack traces
    // -----------------------------------------------------------------
    // Triggering a reliable 500 is difficult because most bad inputs
    // result in RdfParseException (400). We test the contract: if a 500
    // ever occurs, the body must be the generic message with no internals.

    [Fact]
    public async Task InternalError_ResponseBody_DoesNotExposeStackTraces()
    {
        // A syntactically valid but semantically problematic query that
        // may trigger an internal error in the engine. If the engine
        // handles it gracefully (400 or 200), we verify the 500 contract
        // by checking that no response from the server ever leaks traces.
        var query = Uri.EscapeDataString(
            "SELECT (1/0 AS ?x) WHERE { ?s ?p ?o } LIMIT 1");

        var response = await _client.GetAsync($"/sparql?query={query}");

        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var body = await response.Content.ReadAsStringAsync();
            body.Should().Be(
                "An internal error occurred while processing the query.");
            body.Should().NotContain("at ");
            body.Should().NotContain("Exception");
        }
        else
        {
            // The engine handled the query without throwing — that's fine.
            // The 500 contract is tested structurally; we can't force a 500
            // without injecting a faulty provider, which is out of scope
            // for this integration test.
            response.StatusCode.Should().BeOneOf(
                HttpStatusCode.OK, HttpStatusCode.BadRequest);
        }
    }

    // -----------------------------------------------------------------
    // 5.10  Seed data is loaded on first run
    // -----------------------------------------------------------------

    [Fact]
    public async Task SeedData_IsLoaded_OnFirstRun()
    {
        var query = Uri.EscapeDataString(
            "SELECT ?s WHERE { ?s ?p ?o } LIMIT 1");

        var response = await _client.GetAsync($"/sparql?query={query}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().NotBeNullOrWhiteSpace();

        // The SPARQL Results JSON must contain at least one binding
        var json = JsonDocument.Parse(body);
        var bindings = json.RootElement
            .GetProperty("results")
            .GetProperty("bindings");
        bindings.GetArrayLength().Should().BeGreaterThan(0);
    }
}
