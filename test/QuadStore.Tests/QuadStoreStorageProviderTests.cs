using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FluentAssertions;
using TripleStore.Core;
using VDS.RDF;
using VDS.RDF.Parsing.Handlers;
using VDS.RDF.Query;
using VDS.RDF.Storage;
using Xunit;

namespace TripleStore.Tests;

/// <summary>
/// Tests for <see cref="QuadStoreStorageProvider"/>, verifying compliance with the
/// dotNetRDF <see cref="IStorageProvider"/>, <see cref="IQueryableStorage"/>, and
/// <see cref="IUpdateableStorage"/> contracts.
/// </summary>
public class QuadStoreStorageProviderTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────

    private static QuadStore NewStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qsp_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return new QuadStore(dir);
    }

    private static QuadStoreStorageProvider NewProvider(QuadStore? store = null)
        => new QuadStoreStorageProvider(store ?? NewStore());

    private static Uri U(string uri) => new Uri(uri);

    // ── Constructor ──────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullStore_Throws()
    {
        Action act = () => new QuadStoreStorageProvider(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("store");
    }

    [Fact]
    public void Constructor_ValidStore_Succeeds()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);
        provider.Should().NotBeNull();
        provider.Dispose(); // should be a no-op
    }

    // ── IStorageCapabilities ─────────────────────────────────────────────────

    [Fact]
    public void IsReady_ReturnsTrue()
        => NewProvider().IsReady.Should().BeTrue();

    [Fact]
    public void IsReadOnly_ReturnsFalse()
        => NewProvider().IsReadOnly.Should().BeFalse();

    [Fact]
    public void UpdateSupported_ReturnsTrue()
        => NewProvider().UpdateSupported.Should().BeTrue();

    [Fact]
    public void DeleteSupported_ReturnsFalse()
        => NewProvider().DeleteSupported.Should().BeFalse();

    [Fact]
    public void ListGraphsSupported_ReturnsTrue()
        => NewProvider().ListGraphsSupported.Should().BeTrue();

    [Fact]
    public void IOBehaviour_ContainsQuadStoreFlag()
        => (NewProvider().IOBehaviour & IOBehaviour.IsQuadStore).Should().Be(IOBehaviour.IsQuadStore);

    [Fact]
    public void ParentServer_ReturnsNull()
        => NewProvider().ParentServer.Should().BeNull();

    // ── SaveGraph ────────────────────────────────────────────────────────────

    [Fact]
    public void SaveGraph_Null_Throws()
    {
        var provider = NewProvider();
        provider.Invoking(p => p.SaveGraph(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("g");
    }

    [Fact]
    public void SaveGraph_AddsTriplesToStore()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);

        var g = new Graph();
        g.BaseUri = U("http://example.org/g1");
        var factory = new NodeFactory();
        var s = factory.CreateUriNode(U("http://example.org/Ada"));
        var p = factory.CreateUriNode(U("http://example.org/knows"));
        var o = factory.CreateUriNode(U("http://example.org/Bob"));
        g.Assert(new Triple(s, p, o));

        provider.SaveGraph(g);

        var rows = store.Query(graph: "http://example.org/g1").ToList();
        rows.Should().ContainSingle();
        rows[0].subject.Should().Be("http://example.org/Ada");
        rows[0].predicate.Should().Be("http://example.org/knows");
        rows[0].obj.Should().Be("http://example.org/Bob");
    }

    [Fact]
    public void SaveGraph_WithLiterals_StoresCorrectly()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);

        var g = new Graph();
        g.BaseUri = U("http://example.org/g2");
        var factory = new NodeFactory();
        var s = factory.CreateUriNode(U("http://example.org/Ada"));
        var p = factory.CreateUriNode(U("http://example.org/name"));
        var o = factory.CreateLiteralNode("Ada Lovelace", "en");
        g.Assert(new Triple(s, p, o));

        provider.SaveGraph(g);

        var rows = store.Query(graph: "http://example.org/g2").ToList();
        rows.Should().ContainSingle();
        rows[0].obj.Should().Be("\"Ada Lovelace\"@en");
    }

    [Fact]
    public void SaveGraph_WithTypedLiteral_StoresCorrectly()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);

        var g = new Graph();
        g.BaseUri = U("http://example.org/g3");
        var factory = new NodeFactory();
        var s = factory.CreateUriNode(U("http://example.org/item"));
        var p = factory.CreateUriNode(U("http://example.org/count"));
        var o = factory.CreateLiteralNode("42", U("http://www.w3.org/2001/XMLSchema#integer"));
        g.Assert(new Triple(s, p, o));

        provider.SaveGraph(g);

        var rows = store.Query(graph: "http://example.org/g3").ToList();
        rows.Should().ContainSingle();
        rows[0].obj.Should().Be("\"42\"^^<http://www.w3.org/2001/XMLSchema#integer>");
    }

    // ── LoadGraph (IGraph, Uri) ──────────────────────────────────────────────

    [Fact]
    public void LoadGraph_ByUri_Null_IGraph_Throws()
    {
        var provider = NewProvider();
        provider.Invoking(p => p.LoadGraph((IGraph)null!, U("http://example.org/g")))
            .Should().Throw<ArgumentNullException>().WithParameterName("g");
    }

    [Fact]
    public void LoadGraph_ByUri_PopulatesGraph()
    {
        using var store = NewStore();
        store.Append("http://example.org/Ada", "http://example.org/knows", "http://example.org/Bob", "http://example.org/g1");
        store.Append("http://example.org/Ada", "http://example.org/type", "http://example.org/Person", "http://example.org/g2");

        var provider = new QuadStoreStorageProvider(store);
        var g = new Graph();
        provider.LoadGraph(g, U("http://example.org/g1"));

        g.Triples.Should().HaveCount(1);
        g.BaseUri.Should().Be(U("http://example.org/g1"));
    }

    [Fact]
    public void LoadGraph_ByUri_Null_Uri_LoadsAllTriples()
    {
        using var store = NewStore();
        store.Append("http://example.org/Ada", "http://example.org/knows", "http://example.org/Bob", "http://example.org/g1");
        store.Append("http://example.org/Bob", "http://example.org/knows", "http://example.org/Eve", "http://example.org/g2");

        var provider = new QuadStoreStorageProvider(store);
        var g = new Graph();
        provider.LoadGraph(g, (Uri)null!);

        g.Triples.Should().HaveCount(2);
    }

    // ── LoadGraph (IGraph, string) ───────────────────────────────────────────

    [Fact]
    public void LoadGraph_ByString_Null_IGraph_Throws()
    {
        var provider = NewProvider();
        provider.Invoking(p => p.LoadGraph((IGraph)null!, "http://example.org/g"))
            .Should().Throw<ArgumentNullException>().WithParameterName("g");
    }

    [Fact]
    public void LoadGraph_ByString_PopulatesGraph()
    {
        using var store = NewStore();
        store.Append("http://example.org/Ada", "http://example.org/knows", "http://example.org/Bob", "http://example.org/g1");

        var provider = new QuadStoreStorageProvider(store);
        var g = new Graph();
        provider.LoadGraph(g, "http://example.org/g1");

        g.Triples.Should().HaveCount(1);
        g.BaseUri.Should().Be(U("http://example.org/g1"));
    }

    [Fact]
    public void LoadGraph_ByString_HandlesAngularBracketedUri()
    {
        using var store = NewStore();
        // Data stored with angle-bracket format
        store.Append("<http://example.org/S>", "<http://example.org/P>", "<http://example.org/O>", "<http://example.org/g>");

        var provider = new QuadStoreStorageProvider(store);
        var g = new Graph();
        provider.LoadGraph(g, "http://example.org/g");

        g.Triples.Should().HaveCount(1);
    }

    [Fact]
    public void LoadGraph_ByString_EmptyGraph_WhenGraphNotFound()
    {
        using var store = NewStore();
        store.Append("http://example.org/S", "http://example.org/P", "http://example.org/O", "http://example.org/g1");

        var provider = new QuadStoreStorageProvider(store);
        var g = new Graph();
        provider.LoadGraph(g, "http://example.org/nonexistent");

        g.Triples.Should().BeEmpty();
    }

    // ── LoadGraph (IRdfHandler) ──────────────────────────────────────────────

    [Fact]
    public void LoadGraph_ByUri_Null_Handler_Throws()
    {
        var provider = NewProvider();
        provider.Invoking(p => p.LoadGraph((IRdfHandler)null!, U("http://example.org/g")))
            .Should().Throw<ArgumentNullException>().WithParameterName("handler");
    }

    [Fact]
    public void LoadGraph_ByString_Null_Handler_Throws()
    {
        var provider = NewProvider();
        provider.Invoking(p => p.LoadGraph((IRdfHandler)null!, "http://example.org/g"))
            .Should().Throw<ArgumentNullException>().WithParameterName("handler");
    }

    [Fact]
    public void LoadGraph_HandlerOverload_DeliversTriples()
    {
        using var store = NewStore();
        store.Append("http://example.org/Ada", "http://example.org/knows", "http://example.org/Bob", "http://example.org/g1");

        var provider = new QuadStoreStorageProvider(store);
        var g = new Graph();
        var handler = new GraphHandler(g);
        provider.LoadGraph(handler, "http://example.org/g1");

        g.Triples.Should().HaveCount(1);
    }

    // ── UpdateGraph ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdateGraph_ByUri_Additions_AppendsTriples()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);
        var graphUri = U("http://example.org/g");

        var factory = new NodeFactory();
        var triple = new Triple(
            factory.CreateUriNode(U("http://example.org/S")),
            factory.CreateUriNode(U("http://example.org/P")),
            factory.CreateUriNode(U("http://example.org/O")));

        provider.UpdateGraph(graphUri, new[] { triple }, null);

        store.Query(graph: "http://example.org/g").Should().ContainSingle();
    }

    [Fact]
    public void UpdateGraph_ByString_Additions_AppendsTriples()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);

        var factory = new NodeFactory();
        var triple = new Triple(
            factory.CreateUriNode(U("http://example.org/S")),
            factory.CreateUriNode(U("http://example.org/P")),
            factory.CreateUriNode(U("http://example.org/O")));

        provider.UpdateGraph("http://example.org/g", new[] { triple }, null);

        store.Query(graph: "http://example.org/g").Should().ContainSingle();
    }

    [Fact]
    public void UpdateGraph_ByRefNode_Additions_AppendsTriples()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);

        var factory = new NodeFactory();
        var graphNode = factory.CreateUriNode(U("http://example.org/g"));
        var triple = new Triple(
            factory.CreateUriNode(U("http://example.org/S")),
            factory.CreateUriNode(U("http://example.org/P")),
            factory.CreateUriNode(U("http://example.org/O")));

        provider.UpdateGraph(graphNode, new[] { triple }, null);

        store.Query(graph: "http://example.org/g").Should().ContainSingle();
    }

    [Fact]
    public void UpdateGraph_WithRemovals_Throws()
    {
        var provider = NewProvider();
        var factory = new NodeFactory();
        var triple = new Triple(
            factory.CreateUriNode(U("http://example.org/S")),
            factory.CreateUriNode(U("http://example.org/P")),
            factory.CreateUriNode(U("http://example.org/O")));

        provider.Invoking(p => p.UpdateGraph("http://example.org/g", null, new[] { triple }))
            .Should().Throw<RdfStorageException>()
            .WithMessage("*append-only*");
    }

    [Fact]
    public void UpdateGraph_EmptyRemovals_DoesNotThrow()
    {
        var provider = NewProvider();
        provider.Invoking(p => p.UpdateGraph("http://example.org/g", null, Array.Empty<Triple>()))
            .Should().NotThrow();
    }

    // ── DeleteGraph ──────────────────────────────────────────────────────────

    [Fact]
    public void DeleteGraph_ByUri_AlwaysThrows()
    {
        var provider = NewProvider();
        provider.Invoking(p => p.DeleteGraph(U("http://example.org/g")))
            .Should().Throw<RdfStorageException>()
            .WithMessage("*append-only*");
    }

    [Fact]
    public void DeleteGraph_ByString_AlwaysThrows()
    {
        var provider = NewProvider();
        provider.Invoking(p => p.DeleteGraph("http://example.org/g"))
            .Should().Throw<RdfStorageException>()
            .WithMessage("*append-only*");
    }

    // ── ListGraphs / ListGraphNames ──────────────────────────────────────────

    [Fact]
    public void ListGraphs_ReturnsDistinctUris()
    {
        using var store = NewStore();
        store.Append("http://example.org/S1", "http://example.org/P", "http://example.org/O", "http://example.org/g1");
        store.Append("http://example.org/S2", "http://example.org/P", "http://example.org/O", "http://example.org/g2");
        store.Append("http://example.org/S3", "http://example.org/P", "http://example.org/O", "http://example.org/g1");

        var provider = new QuadStoreStorageProvider(store);
        var graphs = provider.ListGraphs().ToList();

        graphs.Should().HaveCount(2);
        graphs.Select(g => g.AbsoluteUri).Should().Contain("http://example.org/g1");
        graphs.Select(g => g.AbsoluteUri).Should().Contain("http://example.org/g2");
    }

    [Fact]
    public void ListGraphs_WithAngledBracketUris_NormalisesAndReturnsUris()
    {
        // Data stored with angle-bracketed graph URIs (as produced by the TriG loader)
        using var store = NewStore();
        store.Append("<http://example.org/S>", "<http://example.org/P>", "<http://example.org/O>", "<http://example.org/g>");

        var provider = new QuadStoreStorageProvider(store);
        var graphs = provider.ListGraphs().ToList();

        // Should return a proper Uri, not a string like "<http://...>"
        graphs.Should().ContainSingle();
        graphs[0].AbsoluteUri.Should().Be("http://example.org/g");
    }

    [Fact]
    public void ListGraphNames_ReturnsDistinctStrings()
    {
        using var store = NewStore();
        store.Append("http://example.org/S", "http://example.org/P", "http://example.org/O", "http://example.org/g1");
        store.Append("http://example.org/S", "http://example.org/P", "http://example.org/O", "http://example.org/g2");

        var provider = new QuadStoreStorageProvider(store);
        var names = provider.ListGraphNames().ToList();

        names.Should().HaveCount(2);
        names.Should().Contain("http://example.org/g1");
        names.Should().Contain("http://example.org/g2");
    }

    [Fact]
    public void ListGraphNames_WithAngledBracketUris_StripsAngleBrackets()
    {
        // Data stored with angle-bracketed graph URIs (as produced by the TriG loader)
        using var store = NewStore();
        store.Append("<http://example.org/S>", "<http://example.org/P>", "<http://example.org/O>", "<http://example.org/g1>");
        store.Append("<http://example.org/S>", "<http://example.org/P>", "<http://example.org/O>", "<http://example.org/g2>");

        var provider = new QuadStoreStorageProvider(store);
        var names = provider.ListGraphNames().ToList();

        names.Should().HaveCount(2);
        names.Should().Contain("http://example.org/g1");
        names.Should().Contain("http://example.org/g2");
        names.Should().NotContain("<http://example.org/g1>");
        names.Should().NotContain("<http://example.org/g2>");
    }

    [Fact]
    public void ListGraphNames_WithMixedUriFormats_NormalisesAll()
    {
        // One graph stored with angle brackets, one without — both should appear as plain URIs.
        using var store = NewStore();
        store.Append("<http://example.org/S>", "<http://example.org/P>", "<http://example.org/O>", "<http://example.org/bracketed>");
        store.Append("http://example.org/S", "http://example.org/P", "http://example.org/O", "http://example.org/plain");

        var provider = new QuadStoreStorageProvider(store);
        var names = provider.ListGraphNames().ToList();

        names.Should().HaveCount(2);
        names.Should().Contain("http://example.org/bracketed");
        names.Should().Contain("http://example.org/plain");
        names.Should().NotContain("<http://example.org/bracketed>");
    }

    [Fact]
    public void ListGraphs_OnEmptyStore_ReturnsEmpty()
    {
        NewProvider().ListGraphs().Should().BeEmpty();
    }

    [Fact]
    public void ListGraphNames_OnEmptyStore_ReturnsEmpty()
    {
        NewProvider().ListGraphNames().Should().BeEmpty();
    }

    // ── IQueryableStorage ────────────────────────────────────────────────────

    [Fact]
    public void Query_NullSparql_Throws()
    {
        var provider = NewProvider();
        provider.Invoking(p => ((IQueryableStorage)p).Query(null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("sparqlQuery");
    }

    [Fact]
    public void Query_SelectReturnsResultSet()
    {
        using var store = NewStore();
        store.Append("http://example.org/Ada", "http://example.org/knows", "http://example.org/Bob", "http://example.org/g");
        var provider = new QuadStoreStorageProvider(store);

        var result = ((IQueryableStorage)provider).Query("SELECT ?s ?o WHERE { ?s <http://example.org/knows> ?o }");

        result.Should().BeOfType<SparqlResultSet>();
        var rs = (SparqlResultSet)result;
        rs.Should().HaveCount(1);
        rs.First()["s"].ToString().Should().Contain("Ada");
        rs.First()["o"].ToString().Should().Contain("Bob");
    }

    [Fact]
    public void Query_AskReturnsTrueWhenMatchFound()
    {
        using var store = NewStore();
        store.Append("http://example.org/S", "http://example.org/P", "http://example.org/O", "http://example.org/g");
        var provider = new QuadStoreStorageProvider(store);

        var result = ((IQueryableStorage)provider).Query("ASK { <http://example.org/S> <http://example.org/P> <http://example.org/O> }");

        result.Should().BeOfType<SparqlResultSet>();
        ((SparqlResultSet)result).Result.Should().BeTrue();
    }

    [Fact]
    public void Query_AskReturnsFalseWhenNoMatch()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);

        var result = ((IQueryableStorage)provider).Query("ASK { <http://example.org/X> <http://example.org/Y> <http://example.org/Z> }");

        result.Should().BeOfType<SparqlResultSet>();
        ((SparqlResultSet)result).Result.Should().BeFalse();
    }

    [Fact]
    public void Query_ConstructReturnsGraph()
    {
        using var store = NewStore();
        store.Append("http://example.org/Ada", "http://example.org/knows", "http://example.org/Bob", "http://example.org/g");
        var provider = new QuadStoreStorageProvider(store);

        var result = ((IQueryableStorage)provider).Query(
            "CONSTRUCT { ?s <http://example.org/knows> ?o } WHERE { ?s <http://example.org/knows> ?o }");

        result.Should().BeAssignableTo<IGraph>();
        var g = (IGraph)result;
        g.Triples.Should().ContainSingle();
    }

    [Fact]
    public void Query_HandlerOverload_NullSparql_Throws()
    {
        var provider = NewProvider();
        provider.Invoking(p => ((IQueryableStorage)p).Query(null!, null!, null!))
            .Should().Throw<ArgumentNullException>().WithParameterName("sparqlQuery");
    }

    [Fact]
    public void Query_HandlerOverload_DeliversResultsToHandler()
    {
        using var store = NewStore();
        store.Append("http://example.org/Ada", "http://example.org/knows", "http://example.org/Bob", "http://example.org/g");
        var provider = new QuadStoreStorageProvider(store);

        var resultSet = new SparqlResultSet();
        var handler = new ResultSetHandler(resultSet);
        ((IQueryableStorage)provider).Query(null, handler, "SELECT ?s WHERE { ?s <http://example.org/knows> ?o }");

        resultSet.Should().HaveCount(1);
    }

    // ── IUpdateableStorage ───────────────────────────────────────────────────

    [Fact]
    public void Update_AlwaysThrows()
    {
        var provider = NewProvider();
        provider.Invoking(p => ((IUpdateableStorage)p).Update("INSERT DATA { <http://s> <http://p> <http://o> }"))
            .Should().Throw<RdfStorageException>()
            .WithMessage("*SPARQL Update*");
    }

    // ── NodeToString / StringToNode round-trip ───────────────────────────────

    [Fact]
    public void RoundTrip_UriNode()
    {
        var factory = new NodeFactory();
        var node = factory.CreateUriNode(U("http://example.org/Ada"));
        var str = QuadStoreStorageProvider.NodeToString(node);
        str.Should().Be("http://example.org/Ada");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<IUriNode>();
        ((IUriNode)back).Uri.Should().Be(U("http://example.org/Ada"));
    }

    [Fact]
    public void RoundTrip_PlainLiteral()
    {
        var factory = new NodeFactory();
        var node = factory.CreateLiteralNode("hello world");
        var str = QuadStoreStorageProvider.NodeToString(node);
        str.Should().Be("\"hello world\"");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<ILiteralNode>();
        ((ILiteralNode)back).Value.Should().Be("hello world");
    }

    [Fact]
    public void RoundTrip_LanguageLiteral()
    {
        var factory = new NodeFactory();
        var node = factory.CreateLiteralNode("hola", "es");
        var str = QuadStoreStorageProvider.NodeToString(node);
        str.Should().Be("\"hola\"@es");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<ILiteralNode>();
        ((ILiteralNode)back).Language.Should().Be("es");
        ((ILiteralNode)back).Value.Should().Be("hola");
    }

    [Fact]
    public void RoundTrip_TypedLiteral()
    {
        var factory = new NodeFactory();
        var node = factory.CreateLiteralNode("42", U("http://www.w3.org/2001/XMLSchema#integer"));
        var str = QuadStoreStorageProvider.NodeToString(node);
        str.Should().Be("\"42\"^^<http://www.w3.org/2001/XMLSchema#integer>");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<ILiteralNode>();
        ((ILiteralNode)back).DataType.Should().Be(U("http://www.w3.org/2001/XMLSchema#integer"));
        ((ILiteralNode)back).Value.Should().Be("42");
    }

    [Fact]
    public void RoundTrip_BlankNode()
    {
        var factory = new NodeFactory();
        var node = factory.CreateBlankNode("myBlank");
        var str = QuadStoreStorageProvider.NodeToString(node);
        str.Should().Be("_:myBlank");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<IBlankNode>();
        ((IBlankNode)back).InternalID.Should().Be("myBlank");
    }

    [Fact]
    public void RoundTrip_LiteralWithSpecialChars()
    {
        var factory = new NodeFactory();
        var node = factory.CreateLiteralNode("line1\nline2");
        var str = QuadStoreStorageProvider.NodeToString(node);
        str.Should().Be("\"line1\\nline2\"");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<ILiteralNode>();
        ((ILiteralNode)back).Value.Should().Be("line1\nline2");
    }

    [Fact]
    public void RoundTrip_LiteralWithBackslashN()
    {
        // Literal is the two-char string backslash + 'n', NOT a newline character.
        var factory = new NodeFactory();
        var node = factory.CreateLiteralNode("value\\ntext");
        var str = QuadStoreStorageProvider.NodeToString(node);
        // EscapeLiteral turns \ into \\ and then the n is untouched → \\n in the stored string.
        str.Should().Be("\"value\\\\ntext\"");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<ILiteralNode>();
        ((ILiteralNode)back).Value.Should().Be("value\\ntext");
    }

    [Fact]
    public void RoundTrip_LiteralWithBackslashR()
    {
        // Literal is backslash + 'r', NOT a carriage-return.
        var factory = new NodeFactory();
        var node = factory.CreateLiteralNode("value\\rtext");
        var str = QuadStoreStorageProvider.NodeToString(node);
        str.Should().Be("\"value\\\\rtext\"");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<ILiteralNode>();
        ((ILiteralNode)back).Value.Should().Be("value\\rtext");
    }

    [Fact]
    public void RoundTrip_LiteralWithBackslashQuote()
    {
        // Literal is backslash + '"'.
        var factory = new NodeFactory();
        var node = factory.CreateLiteralNode("say \\\"hello\\\"");
        var str = QuadStoreStorageProvider.NodeToString(node);
        str.Should().Be("\"say \\\\\\\"hello\\\\\\\"\"");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<ILiteralNode>();
        ((ILiteralNode)back).Value.Should().Be("say \\\"hello\\\"");
    }

    [Fact]
    public void RoundTrip_LiteralWithCarriageReturn()
    {
        var factory = new NodeFactory();
        var node = factory.CreateLiteralNode("line1\rline2");
        var str = QuadStoreStorageProvider.NodeToString(node);
        str.Should().Be("\"line1\\rline2\"");
        var back = QuadStoreStorageProvider.StringToNode(str, factory);
        back.Should().BeAssignableTo<ILiteralNode>();
        ((ILiteralNode)back).Value.Should().Be("line1\rline2");
    }

    // ── StringToNode edge cases ───────────────────────────────────────────────

    [Fact]
    public void StringToNode_NullOrEmpty_ReturnsBlankNode()
    {
        var factory = new NodeFactory();
        QuadStoreStorageProvider.StringToNode(null!, factory).Should().BeAssignableTo<IBlankNode>();
        QuadStoreStorageProvider.StringToNode(string.Empty, factory).Should().BeAssignableTo<IBlankNode>();
    }

    [Fact]
    public void StringToNode_AngledBracketUri_ReturnsUriNode()
    {
        var factory = new NodeFactory();
        var node = QuadStoreStorageProvider.StringToNode("<http://example.org/test>", factory);
        node.Should().BeAssignableTo<IUriNode>();
        ((IUriNode)node).Uri.Should().Be(U("http://example.org/test"));
    }

    [Fact]
    public void StringToNode_FallbackNonUri_ReturnsLiteralNode()
    {
        var factory = new NodeFactory();
        // Something that looks like neither a URI nor a quoted literal
        var node = QuadStoreStorageProvider.StringToNode("just-some-text", factory);
        node.Should().BeAssignableTo<ILiteralNode>();
    }

    // ── Integration: SaveGraph → LoadGraph round-trip ────────────────────────

    [Fact]
    public void SaveAndLoad_RoundTrip_PreservesTriples()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);
        var graphUri = U("http://example.org/rt");

        var original = new Graph();
        original.BaseUri = graphUri;
        var factory = new NodeFactory();
        original.Assert(new Triple(
            factory.CreateUriNode(U("http://example.org/Ada")),
            factory.CreateUriNode(U("http://example.org/knows")),
            factory.CreateUriNode(U("http://example.org/Bob"))));
        original.Assert(new Triple(
            factory.CreateUriNode(U("http://example.org/Ada")),
            factory.CreateUriNode(U("http://example.org/name")),
            factory.CreateLiteralNode("Ada Lovelace", "en")));

        provider.SaveGraph(original);

        var loaded = new Graph();
        provider.LoadGraph(loaded, graphUri);

        loaded.Triples.Should().HaveCount(2);
        loaded.BaseUri.Should().Be(graphUri);
    }

    // ── Integration: UpdateGraph → LoadGraph ─────────────────────────────────

    [Fact]
    public void UpdateGraph_ThenLoad_ReflectsAdditions()
    {
        using var store = NewStore();
        var provider = new QuadStoreStorageProvider(store);
        var graphUri = U("http://example.org/ug");
        var factory = new NodeFactory();

        var t1 = new Triple(
            factory.CreateUriNode(U("http://example.org/S1")),
            factory.CreateUriNode(U("http://example.org/P")),
            factory.CreateUriNode(U("http://example.org/O1")));
        var t2 = new Triple(
            factory.CreateUriNode(U("http://example.org/S2")),
            factory.CreateUriNode(U("http://example.org/P")),
            factory.CreateUriNode(U("http://example.org/O2")));

        provider.UpdateGraph(graphUri, new[] { t1 }, null);
        provider.UpdateGraph(graphUri, new[] { t2 }, null);

        var loaded = new Graph();
        provider.LoadGraph(loaded, graphUri);
        loaded.Triples.Should().HaveCount(2);
    }
}
