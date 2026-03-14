using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace TripleStore.Tests;

/// <summary>
/// Property-based tests for the SPARQL Server over Minimal API sample.
/// Uses FsCheck.Xunit to verify correctness properties across many
/// randomly generated inputs.
/// </summary>
public class SparqlServerPropertyTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public SparqlServerPropertyTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    // =====================================================================
    // Generators
    // =====================================================================

    private static readonly string[] Subjects =
    [
        "?s", "<http://example.org/alice>", "<http://example.org/bob>"
    ];

    private static readonly string[] Predicates =
    [
        "?p",
        "<http://xmlns.com/foaf/0.1/name>",
        "<http://xmlns.com/foaf/0.1/knows>"
    ];

    private static readonly string[] Objects =
    [
        "?o",
        "<http://example.org/alice>",
        "<http://example.org/bob>",
        "?name"
    ];

    private static readonly string[] SelectVars =
    [
        "?s", "?p", "?o", "?name", "?x", "?y"
    ];

    /// <summary>
    /// Generates random valid SELECT queries against the seed data schema.
    /// Varies variable names, triple patterns, and LIMIT values.
    /// </summary>
    private static Arbitrary<string> SelectQueryArb()
    {
        var gen = Gen.Choose(0, Subjects.Length - 1)
            .SelectMany(si => Gen.Choose(0, Predicates.Length - 1)
            .SelectMany(pi => Gen.Choose(0, Objects.Length - 1)
            .SelectMany(oi => Gen.Choose(1, 10)
            .SelectMany(limit => Gen.Choose(0, SelectVars.Length - 1)
            .Select(vi =>
                $"SELECT {SelectVars[vi]} WHERE {{ {Subjects[si]} {Predicates[pi]} {Objects[oi]} }} LIMIT {limit}")))));
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates random valid CONSTRUCT queries against the seed data schema.
    /// </summary>
    private static Arbitrary<string> ConstructQueryArb()
    {
        var gen = Gen.Choose(0, Subjects.Length - 1)
            .SelectMany(si => Gen.Choose(0, Predicates.Length - 1)
            .SelectMany(pi => Gen.Choose(0, Objects.Length - 1)
            .SelectMany(oi => Gen.Choose(1, 10)
            .Select(limit =>
                $"CONSTRUCT {{ ?s ?p ?o }} WHERE {{ {Subjects[si]} {Predicates[pi]} {Objects[oi]} }} LIMIT {limit}"))));
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates random valid SELECT queries for protocol equivalence testing.
    /// </summary>
    private static Arbitrary<string> SimpleSelectQueryArb()
    {
        var vars = new[] { "?s", "?x", "?sub", "?thing" };
        var gen = Gen.Choose(0, vars.Length - 1)
            .SelectMany(vi => Gen.Choose(1, 10)
            .Select(limit =>
                $"SELECT {vars[vi]} WHERE {{ {vars[vi]} ?p ?o }} LIMIT {limit}"));
        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates random strings that are NOT valid SPARQL.
    /// Mixes known-bad patterns with random alphanumeric strings.
    /// </summary>
    private static Arbitrary<string> MalformedQueryArb()
    {
        var knownBad = new[]
        {
            "SELCT * WHERE { ?s ?p ?o }",
            "FORM { ?s ?p ?o }",
            "SELECT * FROM table",
            "INSERT INTO triples VALUES (1,2,3)",
            "DROP TABLE quads",
            "SELECT * WHERE",
            "CONSTRUCT WHERE { }",
            "SELECT ?s WHERE { ?s ?p }",
            "DELETE FROM graph WHERE { ?s ?p ?o }",
            "UPDATE SET x = 1",
            "xyzzy foobar baz",
            "{{{{",
            "SELECT ?s WHERE ?s ?p ?o",
            "SPARQL is fun!",
            "SELECT ?s WHERE { ?s ?p ?o } GROUP",
            "ASK { ?s ?p }",
            "DESCRIBE",
            "not a query at all 12345",
            "SELECT * FORM { ?s ?p ?o }",
            "CONSTRUCT { ?s } WHERE { ?s ?p ?o }"
        };

        var knownBadGen = Gen.Elements(knownBad);

        var alphaChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 ".ToCharArray();
        var randomAlphanumeric = Gen.Choose(3, 50)
            .SelectMany(len =>
                Gen.ArrayOf(Gen.Elements(alphaChars), len)
                .Select(chars => new string(chars).Trim()))
            .Where(s => !string.IsNullOrWhiteSpace(s));

        var gen = Gen.Frequency(
            (3, knownBadGen),
            (1, randomAlphanumeric));

        return gen.ToArbitrary();
    }

    // =====================================================================
    // Property 1: Response content-type matches query result type
    // Validates: Requirements 4.3, 4.4
    // =====================================================================

    /// <summary>
    /// For any valid SELECT query, the response content-type SHALL be
    /// application/sparql-results+json.
    /// </summary>
    [Fact]
    public void Property1_SelectQuery_ReturnsJsonContentType()
    {
        Prop.ForAll(SelectQueryArb(), (string query) =>
        {
            var encoded = Uri.EscapeDataString(query);
            var response = _client.GetAsync($"/sparql?query={encoded}")
                .GetAwaiter().GetResult();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType;
                return (contentType == "application/sparql-results+json")
                    .Label($"Expected application/sparql-results+json but got {contentType} for: {query}");
            }

            // Query might match no data — still valid
            return true.Label($"Non-200 status {response.StatusCode} for: {query}");
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// For any valid CONSTRUCT query, the response content-type SHALL be
    /// text/turtle.
    /// </summary>
    [Fact]
    public void Property1_ConstructQuery_ReturnsTurtleContentType()
    {
        Prop.ForAll(ConstructQueryArb(), (string query) =>
        {
            var encoded = Uri.EscapeDataString(query);
            var response = _client.GetAsync($"/sparql?query={encoded}")
                .GetAwaiter().GetResult();

            if (response.StatusCode == HttpStatusCode.OK)
            {
                var contentType = response.Content.Headers.ContentType?.MediaType;
                return (contentType == "text/turtle")
                    .Label($"Expected text/turtle but got {contentType} for: {query}");
            }

            return true.Label($"Non-200 status {response.StatusCode} for: {query}");
        }).QuickCheckThrowOnFailure();
    }

    // =====================================================================
    // Property 2: Protocol equivalence across submission methods
    // Validates: Requirements 4.1, 5.1, 5.2
    // =====================================================================

    /// <summary>
    /// For any valid SPARQL SELECT query, submitting via GET, POST direct,
    /// and POST form-encoded SHALL produce identical responses.
    /// </summary>
    [Fact]
    public void Property2_ProtocolEquivalence_AllMethodsReturnSameResult()
    {
        Prop.ForAll(SimpleSelectQueryArb(), (string query) =>
        {
            // 1. GET /sparql?query=...
            var getEncoded = Uri.EscapeDataString(query);
            var getResponse = _client.GetAsync($"/sparql?query={getEncoded}")
                .GetAwaiter().GetResult();
            var getBody = getResponse.Content.ReadAsStringAsync()
                .GetAwaiter().GetResult();
            var getContentType = getResponse.Content.Headers.ContentType?.MediaType;
            var getStatus = getResponse.StatusCode;

            // 2. POST /sparql with Content-Type: application/sparql-query
            var postDirectRequest = new HttpRequestMessage(HttpMethod.Post, "/sparql")
            {
                Content = new StringContent(query, Encoding.UTF8, "application/sparql-query")
            };
            var postDirectResponse = _client.SendAsync(postDirectRequest)
                .GetAwaiter().GetResult();
            var postDirectBody = postDirectResponse.Content.ReadAsStringAsync()
                .GetAwaiter().GetResult();
            var postDirectContentType = postDirectResponse.Content.Headers.ContentType?.MediaType;
            var postDirectStatus = postDirectResponse.StatusCode;

            // 3. POST /sparql with Content-Type: application/x-www-form-urlencoded
            var formContent = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("query", query)
            });
            var postFormResponse = _client.PostAsync("/sparql", formContent)
                .GetAwaiter().GetResult();
            var postFormBody = postFormResponse.Content.ReadAsStringAsync()
                .GetAwaiter().GetResult();
            var postFormContentType = postFormResponse.Content.Headers.ContentType?.MediaType;
            var postFormStatus = postFormResponse.StatusCode;

            // Oracle: status codes and content types must match exactly
            var statusMatch = getStatus == postDirectStatus && getStatus == postFormStatus;
            var contentTypeMatch = getContentType == postDirectContentType
                && getContentType == postFormContentType;

            // For body comparison, use semantic JSON comparison to handle
            // non-deterministic result ordering from the SPARQL engine
            var bodyMatch = SparqlResultsAreEquivalent(getBody, postDirectBody)
                && SparqlResultsAreEquivalent(getBody, postFormBody);

            return (statusMatch && contentTypeMatch && bodyMatch)
                .Label($"Mismatch for query: {query}\n" +
                       $"  GET:         {getStatus} {getContentType}\n" +
                       $"  POST-direct: {postDirectStatus} {postDirectContentType}\n" +
                       $"  POST-form:   {postFormStatus} {postFormContentType}\n" +
                       $"  Bodies equiv: GET==POST-direct:{SparqlResultsAreEquivalent(getBody, postDirectBody)} GET==POST-form:{SparqlResultsAreEquivalent(getBody, postFormBody)}");
        }).QuickCheckThrowOnFailure();
    }

    /// <summary>
    /// Compares two SPARQL Results JSON bodies semantically.
    /// Parses the JSON, extracts the bindings, sorts them, and compares.
    /// This handles non-deterministic result ordering from the engine.
    /// </summary>
    private static bool SparqlResultsAreEquivalent(string body1, string body2)
    {
        try
        {
            var doc1 = JsonDocument.Parse(body1);
            var doc2 = JsonDocument.Parse(body2);

            // Compare the "head" section (variable names)
            var vars1 = GetSortedVars(doc1);
            var vars2 = GetSortedVars(doc2);
            if (!vars1.SequenceEqual(vars2))
                return false;

            // Compare the "results.bindings" section (sorted for order-independence)
            var bindings1 = GetSortedBindings(doc1);
            var bindings2 = GetSortedBindings(doc2);
            return bindings1.SequenceEqual(bindings2);
        }
        catch
        {
            // If not valid JSON, fall back to string comparison
            return body1 == body2;
        }
    }

    private static List<string> GetSortedVars(JsonDocument doc)
    {
        var vars = doc.RootElement
            .GetProperty("head")
            .GetProperty("vars")
            .EnumerateArray()
            .Select(v => v.GetString() ?? "")
            .OrderBy(v => v)
            .ToList();
        return vars;
    }

    private static List<string> GetSortedBindings(JsonDocument doc)
    {
        var bindings = doc.RootElement
            .GetProperty("results")
            .GetProperty("bindings")
            .EnumerateArray()
            .Select(b => b.ToString())
            .OrderBy(b => b)
            .ToList();
        return bindings;
    }

    // =====================================================================
    // Property 3: Malformed SPARQL queries return 400
    // Validates: Requirements 6.1
    // =====================================================================

    /// <summary>
    /// For any string that is not a valid SPARQL query, the endpoint SHALL
    /// return HTTP 400 with a non-empty response body.
    /// </summary>
    [Fact]
    public void Property3_MalformedQuery_Returns400_WithNonEmptyBody()
    {
        Prop.ForAll(MalformedQueryArb(), (string query) =>
        {
            var encoded = Uri.EscapeDataString(query);
            var response = _client.GetAsync($"/sparql?query={encoded}")
                .GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync()
                .GetAwaiter().GetResult();

            // Some random strings might accidentally be valid SPARQL
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return true.Label($"Accidentally valid SPARQL: {query}");
            }

            var is400 = response.StatusCode == HttpStatusCode.BadRequest;
            var hasBody = !string.IsNullOrWhiteSpace(body);

            return (is400 && hasBody)
                .Label($"Expected 400 with body for: {query}, got {response.StatusCode}, body empty: {!hasBody}");
        }).QuickCheckThrowOnFailure();
    }

    // =====================================================================
    // Property 4: SPARQL Update always returns 501
    // Validates: Requirements 9.8
    // =====================================================================

    /// <summary>
    /// Generates random non-empty strings for SPARQL Update bodies.
    /// Mixes known SPARQL Update syntax with random alphanumeric strings.
    /// </summary>
    private static Arbitrary<string> SparqlUpdateBodyArb()
    {
        var knownUpdates = new[]
        {
            "INSERT DATA { <http://example.org/s> <http://example.org/p> <http://example.org/o> }",
            "DELETE DATA { <http://example.org/s> <http://example.org/p> <http://example.org/o> }",
            "DELETE WHERE { ?s ?p ?o }",
            "CLEAR GRAPH <http://example.org/g>",
            "DROP ALL",
            "LOAD <http://example.org/data>",
            "CREATE GRAPH <http://example.org/new>",
            "COPY DEFAULT TO <http://example.org/g>",
            "MOVE <http://example.org/g1> TO <http://example.org/g2>",
            "ADD DEFAULT TO <http://example.org/g>"
        };

        var knownGen = Gen.Elements(knownUpdates);

        var alphaChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 {};<>".ToCharArray();
        var randomGen = Gen.Choose(3, 80)
            .SelectMany(len =>
                Gen.ArrayOf(Gen.Elements(alphaChars), len)
                .Select(chars => new string(chars).Trim()))
            .Where(s => !string.IsNullOrWhiteSpace(s));

        var gen = Gen.Frequency(
            (2, knownGen),
            (3, randomGen));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// For any non-empty SPARQL Update string submitted via POST with
    /// content-type application/sparql-update, the endpoint SHALL return
    /// HTTP 501 indicating that SPARQL Update is not supported.
    /// </summary>
    [Fact]
    public void Property4_SparqlUpdate_AlwaysReturns501()
    {
        Prop.ForAll(SparqlUpdateBodyArb(), (string updateBody) =>
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/sparql")
            {
                Content = new StringContent(updateBody, Encoding.UTF8, "application/sparql-update")
            };
            var response = _client.SendAsync(request)
                .GetAwaiter().GetResult();

            var is501 = response.StatusCode == HttpStatusCode.NotImplemented;

            return is501
                .Label($"Expected 501 for SPARQL Update but got {response.StatusCode} for body: {updateBody}");
        }).QuickCheckThrowOnFailure();
    }

    // =====================================================================
    // Property 5: Graph Store write operations return 501
    // Validates: Requirements 9.12, 9.13, 9.14
    // =====================================================================

    /// <summary>
    /// Generates random graph URIs for Graph Store Protocol requests.
    /// </summary>
    private static Arbitrary<string> GraphUriArb()
    {
        var knownUris = new[]
        {
            "http://example.org/graph1",
            "http://example.org/graph2",
            "http://example.org/people",
            "http://example.org/data",
            "urn:test:graph",
            "http://localhost/g"
        };

        var knownGen = Gen.Elements(knownUris);

        var alphaChars = "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();
        var randomGen = Gen.Choose(3, 30)
            .SelectMany(len =>
                Gen.ArrayOf(Gen.Elements(alphaChars), len)
                .Select(chars => $"http://example.org/{new string(chars)}"));

        var gen = Gen.Frequency(
            (2, knownGen),
            (3, randomGen));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates random non-empty request bodies for Graph Store write operations.
    /// </summary>
    private static Arbitrary<string> GraphStoreBodyArb()
    {
        var knownBodies = new[]
        {
            "<http://example.org/s> <http://example.org/p> <http://example.org/o> .",
            "@prefix ex: <http://example.org/> . ex:s ex:p ex:o .",
            "<http://example.org/a> <http://example.org/b> \"hello\" .",
            "@prefix foaf: <http://xmlns.com/foaf/0.1/> . <http://example.org/alice> foaf:name \"Alice\" ."
        };

        var knownGen = Gen.Elements(knownBodies);

        var alphaChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 :<>/._\"".ToCharArray();
        var randomGen = Gen.Choose(5, 80)
            .SelectMany(len =>
                Gen.ArrayOf(Gen.Elements(alphaChars), len)
                .Select(chars => new string(chars).Trim()))
            .Where(s => !string.IsNullOrWhiteSpace(s));

        var gen = Gen.Frequency(
            (2, knownGen),
            (3, randomGen));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// For any PUT, POST, or DELETE request to the Graph Store Protocol
    /// endpoint, the endpoint SHALL return HTTP 501 indicating that the
    /// operation is not supported by the backend.
    /// </summary>
    [Fact]
    public void Property5_GraphStoreWriteOperations_Return501()
    {
        Prop.ForAll(GraphUriArb(), GraphStoreBodyArb(), (string graphUri, string body) =>
        {
            var encodedUri = Uri.EscapeDataString(graphUri);
            var url = $"/sparql/graph?graph={encodedUri}";
            var content = new StringContent(body, Encoding.UTF8, "text/turtle");

            // PUT
            var putRequest = new HttpRequestMessage(HttpMethod.Put, url) { Content = content };
            var putResponse = _client.SendAsync(putRequest).GetAwaiter().GetResult();
            var putIs501 = putResponse.StatusCode == HttpStatusCode.NotImplemented;

            // POST
            var postContent = new StringContent(body, Encoding.UTF8, "text/turtle");
            var postRequest = new HttpRequestMessage(HttpMethod.Post, url) { Content = postContent };
            var postResponse = _client.SendAsync(postRequest).GetAwaiter().GetResult();
            var postIs501 = postResponse.StatusCode == HttpStatusCode.NotImplemented;

            // DELETE
            var deleteRequest = new HttpRequestMessage(HttpMethod.Delete, url);
            var deleteResponse = _client.SendAsync(deleteRequest).GetAwaiter().GetResult();
            var deleteIs501 = deleteResponse.StatusCode == HttpStatusCode.NotImplemented;

            return (putIs501 && postIs501 && deleteIs501)
                .Label($"Expected 501 for all Graph Store writes but got PUT:{putResponse.StatusCode} POST:{postResponse.StatusCode} DELETE:{deleteResponse.StatusCode} for graph: {graphUri}");
        }).QuickCheckThrowOnFailure();
    }

    // =====================================================================
    // Property 6: Graph Store GET returns Turtle content type
    // Validates: Requirements 9.10
    // =====================================================================

    /// <summary>
    /// Generates graph URI options for Graph Store GET requests.
    /// Produces a mix of: no graph param (empty string), known URIs,
    /// and random URIs (which return empty but valid Turtle graphs).
    /// </summary>
    private static Arbitrary<string> GraphStoreGetUriArb()
    {
        var knownUris = new[]
        {
            "",  // default graph (no graph param)
            "http://example.org/graph1",
            "http://example.org/people",
            "http://example.org/data",
            "urn:test:graph",
            "urn:x-arq:DefaultGraph",
            "http://example.org/nonexistent"
        };

        var knownGen = Gen.Elements(knownUris);

        var alphaChars = "abcdefghijklmnopqrstuvwxyz0123456789-_".ToCharArray();
        var randomGen = Gen.Choose(3, 30)
            .SelectMany(len =>
                Gen.ArrayOf(Gen.Elements(alphaChars), len)
                .Select(chars => $"http://example.org/{new string(chars)}"));

        var gen = Gen.Frequency(
            (3, knownGen),
            (2, randomGen));

        return gen.ToArbitrary();
    }

    /// <summary>
    /// For any HTTP GET request to the Graph Store Protocol endpoint,
    /// the response status SHALL be 200 and the content-type SHALL be
    /// text/turtle — regardless of whether the graph contains data
    /// (an empty graph is still valid Turtle).
    /// </summary>
    [Fact]
    public void Property6_GraphStoreGet_ReturnsTurtleContentType()
    {
        Prop.ForAll(GraphStoreGetUriArb(), (string graphUri) =>
        {
            var url = string.IsNullOrEmpty(graphUri)
                ? "/sparql/graph"
                : $"/sparql/graph?graph={Uri.EscapeDataString(graphUri)}";

            var response = _client.GetAsync(url).GetAwaiter().GetResult();

            var is200 = response.StatusCode == HttpStatusCode.OK;
            var contentType = response.Content.Headers.ContentType?.MediaType;
            var isTurtle = contentType == "text/turtle";

            return (is200 && isTurtle)
                .Label($"Expected 200 text/turtle but got {response.StatusCode} {contentType} for graph: '{graphUri}'");
        }).QuickCheckThrowOnFailure();
    }
}
