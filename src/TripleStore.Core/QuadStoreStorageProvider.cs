using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Storage;
using VDS.RDF.Storage.Management;

namespace TripleStore.Core;

/// <summary>
/// Adapter that exposes <see cref="QuadStore"/> as a dotNetRDF <see cref="IStorageProvider"/>,
/// enabling compatibility with the Leviathan SPARQL query engine and other dotNetRDF tooling.
/// </summary>
/// <remarks>
/// <para>This implementation maps dotNetRDF storage operations to QuadStore's underlying
/// columnar bitmap store. Quad support is enabled: named graphs are surfaced as required
/// by dotNetRDF.</para>
/// <para><b>Limitations:</b></para>
/// <list type="bullet">
///   <item><description>Graph deletion is not supported (QuadStore is append-only).</description></item>
///   <item><description>Triple removal via
///   <see cref="UpdateGraph(IRefNode, IEnumerable{Triple}, IEnumerable{Triple})"/> is not supported.
///   </description></item>
///   <item><description>SPARQL Update via <see cref="Update"/> is not supported.</description></item>
/// </list>
/// </remarks>
/// <example>
/// <code>
/// // Create or open a QuadStore
/// using var qs = new QuadStore("/path/to/data");
///
/// // Wrap in the storage provider adapter
/// IStorageProvider provider = new QuadStoreStorageProvider(qs);
///
/// // Load a named graph
/// var g = new Graph();
/// provider.LoadGraph(g, new Uri("http://example.org/mygraph"));
///
/// // Use with the Leviathan SPARQL query engine
/// var queryProvider = (IQueryableStorage)provider;
/// var results = (SparqlResultSet)queryProvider.Query("SELECT * WHERE { ?s ?p ?o }");
/// </code>
/// </example>
public sealed class QuadStoreStorageProvider : IStorageProvider, IQueryableStorage, IUpdateableStorage
{
    private readonly QuadStore _store;

    /// <summary>
    /// Initializes a new instance of <see cref="QuadStoreStorageProvider"/>.
    /// </summary>
    /// <param name="store">The underlying <see cref="QuadStore"/> instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="store"/> is null.</exception>
    public QuadStoreStorageProvider(QuadStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    // ── IStorageCapabilities ────────────────────────────────────────────────

    /// <inheritdoc/>
    public bool IsReady => true;

    /// <inheritdoc/>
    public bool IsReadOnly => false;

    /// <inheritdoc/>
    public IOBehaviour IOBehaviour =>
        IOBehaviour.IsQuadStore |
        IOBehaviour.HasNamedGraphs |
        IOBehaviour.HasDefaultGraph |
        IOBehaviour.AppendTriples |
        IOBehaviour.CanUpdateAddTriples;

    /// <inheritdoc/>
    public bool UpdateSupported => true;

    /// <summary>
    /// Returns <see langword="false"/>. QuadStore is append-only and does not support graph deletion.
    /// </summary>
    public bool DeleteSupported => false;

    /// <inheritdoc/>
    public bool ListGraphsSupported => true;

    // ── IStorageProvider ────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="null"/>. QuadStore is a standalone store with no parent server.
    /// </summary>
    public IStorageServer ParentServer => null;

    /// <inheritdoc/>
    public void LoadGraph(IGraph g, Uri graphUri)
    {
        if (g == null) throw new ArgumentNullException(nameof(g));
        LoadGraphInternal(g, graphUri?.AbsoluteUri);
        if (graphUri != null)
            g.BaseUri = graphUri;
    }

    /// <inheritdoc/>
    public void LoadGraph(IGraph g, string graphUri)
    {
        if (g == null) throw new ArgumentNullException(nameof(g));
        LoadGraphInternal(g, graphUri);
        if (graphUri != null && Uri.TryCreate(graphUri, UriKind.Absolute, out var uri))
            g.BaseUri = uri;
    }

    /// <inheritdoc/>
    public void LoadGraph(IRdfHandler handler, Uri graphUri)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        LoadGraphHandlerInternal(handler, graphUri?.AbsoluteUri);
    }

    /// <inheritdoc/>
    public void LoadGraph(IRdfHandler handler, string graphUri)
    {
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        LoadGraphHandlerInternal(handler, graphUri);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The graph is saved using its <see cref="IGraph.Name"/> URI (if set) or
    /// <see cref="INodeFactory.BaseUri"/> as the named graph identifier.
    /// If neither is available, an empty string is used as the graph identifier.
    /// </remarks>
    public void SaveGraph(IGraph g)
    {
        if (g == null) throw new ArgumentNullException(nameof(g));
        string graphUri = g.Name is IUriNode uriNode
            ? uriNode.Uri.AbsoluteUri
            : g.BaseUri?.AbsoluteUri ?? string.Empty;

        foreach (var triple in g.Triples)
        {
            _store.Append(
                NodeToString(triple.Subject),
                NodeToString(triple.Predicate),
                NodeToString(triple.Object),
                graphUri);
        }
    }

    /// <inheritdoc/>
    /// <remarks>Removals are not supported; pass <see langword="null"/> or an empty collection.</remarks>
    public void UpdateGraph(IRefNode graphName, IEnumerable<Triple> additions, IEnumerable<Triple> removals)
    {
        string graphUri = graphName is IUriNode un ? un.Uri.AbsoluteUri : string.Empty;
        UpdateGraphInternal(graphUri, additions, removals);
    }

    /// <inheritdoc/>
    /// <remarks>Removals are not supported; pass <see langword="null"/> or an empty collection.</remarks>
    public void UpdateGraph(Uri graphUri, IEnumerable<Triple> additions, IEnumerable<Triple> removals)
    {
        UpdateGraphInternal(graphUri?.AbsoluteUri ?? string.Empty, additions, removals);
    }

    /// <inheritdoc/>
    /// <remarks>Removals are not supported; pass <see langword="null"/> or an empty collection.</remarks>
    public void UpdateGraph(string graphUri, IEnumerable<Triple> additions, IEnumerable<Triple> removals)
    {
        UpdateGraphInternal(graphUri ?? string.Empty, additions, removals);
    }

    /// <summary>
    /// Not supported. QuadStore is append-only and does not support graph deletion.
    /// </summary>
    /// <exception cref="RdfStorageException">Always thrown.</exception>
    public void DeleteGraph(Uri graphUri)
        => throw new RdfStorageException("QuadStore is append-only and does not support graph deletion.");

    /// <summary>
    /// Not supported. QuadStore is append-only and does not support graph deletion.
    /// </summary>
    /// <exception cref="RdfStorageException">Always thrown.</exception>
    public void DeleteGraph(string graphUri)
        => throw new RdfStorageException("QuadStore is append-only and does not support graph deletion.");

    /// <inheritdoc/>
    public IEnumerable<Uri> ListGraphs()
    {
        return _store.Query()
            .Select(q => q.graph)
            .Distinct()
            .Where(g => Uri.TryCreate(g, UriKind.Absolute, out _))
            .Select(g => new Uri(g));
    }

    /// <inheritdoc/>
    public IEnumerable<string> ListGraphNames()
    {
        return _store.Query()
            .Select(q => q.graph)
            .Distinct();
    }

    // ── IQueryableStorage ───────────────────────────────────────────────────

    /// <summary>
    /// Executes a SPARQL SELECT, ASK, CONSTRUCT, or DESCRIBE query over all graphs in the store
    /// using the dotNetRDF Leviathan in-memory SPARQL engine.
    /// </summary>
    /// <param name="sparqlQuery">The SPARQL query string.</param>
    /// <returns>
    /// A <see cref="SparqlResultSet"/> for SELECT/ASK queries, or an <see cref="IGraph"/>
    /// for CONSTRUCT/DESCRIBE queries.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sparqlQuery"/> is null.</exception>
    public object Query(string sparqlQuery)
    {
        if (sparqlQuery == null) throw new ArgumentNullException(nameof(sparqlQuery));
        var processor = CreateQueryProcessor();
        var query = new SparqlQueryParser().ParseFromString(sparqlQuery);
        return processor.ProcessQuery(query);
    }

    /// <summary>
    /// Executes a SPARQL query, delivering results to the supplied handlers using the
    /// dotNetRDF Leviathan in-memory SPARQL engine.
    /// </summary>
    /// <param name="rdfHandler">Handler for CONSTRUCT/DESCRIBE results (graph results).</param>
    /// <param name="resultsHandler">Handler for SELECT/ASK results (tabular results).</param>
    /// <param name="sparqlQuery">The SPARQL query string.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="sparqlQuery"/> is null.</exception>
    public void Query(IRdfHandler rdfHandler, ISparqlResultsHandler resultsHandler, string sparqlQuery)
    {
        if (sparqlQuery == null) throw new ArgumentNullException(nameof(sparqlQuery));
        var processor = CreateQueryProcessor();
        var query = new SparqlQueryParser().ParseFromString(sparqlQuery);
        processor.ProcessQuery(rdfHandler, resultsHandler, query);
    }

    // ── IUpdateableStorage ──────────────────────────────────────────────────

    /// <summary>
    /// Not supported. QuadStore is append-only; use <see cref="SaveGraph"/> or
    /// <see cref="UpdateGraph(Uri, IEnumerable{Triple}, IEnumerable{Triple})"/> to add triples.
    /// </summary>
    /// <exception cref="RdfStorageException">Always thrown.</exception>
    public void Update(string sparqlUpdate)
        => throw new RdfStorageException(
            "QuadStore does not support SPARQL Update (append-only store). " +
            "Use SaveGraph or UpdateGraph to add triples.");

    // ── IDisposable ─────────────────────────────────────────────────────────

    /// <summary>
    /// Disposes this adapter.
    /// The lifecycle of the underlying <see cref="QuadStore"/> is managed by the caller.
    /// </summary>
    public void Dispose()
    {
        // QuadStore lifecycle is managed externally; nothing to dispose here.
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void LoadGraphInternal(IGraph g, string graphUri)
    {
        foreach (var (s, p, o, _) in QueryByGraph(graphUri))
        {
            g.Assert(new Triple(
                StringToNode(s, g),
                StringToNode(p, g),
                StringToNode(o, g)));
        }
    }

    private void LoadGraphHandlerInternal(IRdfHandler handler, string graphUri)
    {
        var factory = new NodeFactory();
        handler.StartRdf();
        try
        {
            foreach (var (s, p, o, _) in QueryByGraph(graphUri))
            {
                handler.HandleTriple(new Triple(
                    StringToNode(s, factory),
                    StringToNode(p, factory),
                    StringToNode(o, factory)));
            }
            handler.EndRdf(true);
        }
        catch
        {
            handler.EndRdf(false);
            throw;
        }
    }

    private void UpdateGraphInternal(string graphUri, IEnumerable<Triple> additions, IEnumerable<Triple> removals)
    {
        if (removals != null)
        {
            var removalList = removals.ToList();
            if (removalList.Count > 0)
                throw new RdfStorageException(
                    "QuadStore is append-only and does not support triple removal.");
        }

        if (additions != null)
        {
            foreach (var triple in additions)
            {
                _store.Append(
                    NodeToString(triple.Subject),
                    NodeToString(triple.Predicate),
                    NodeToString(triple.Object),
                    graphUri);
            }
        }
    }

    /// <summary>
    /// Queries the store for triples in the given named graph, trying both the plain URI and
    /// the angle-bracketed form to cover data inserted in either format.
    /// </summary>
    private IEnumerable<(string s, string p, string o, string g)> QueryByGraph(string graphUri)
    {
        if (graphUri == null)
            return _store.Query();

        // Normalise: derive both the plain and angle-bracketed form of the URI.
        string plain = graphUri.StartsWith("<") && graphUri.EndsWith(">")
            ? graphUri.Substring(1, graphUri.Length - 2)
            : graphUri;
        string bracketed = $"<{plain}>";

        var seen = new HashSet<(string, string, string, string)>();
        IEnumerable<(string, string, string, string)> Merge()
        {
            foreach (var row in _store.Query(graph: plain))
                if (seen.Add(row)) yield return row;
            foreach (var row in _store.Query(graph: bracketed))
                if (seen.Add(row)) yield return row;
        }
        return Merge();
    }

    private LeviathanQueryProcessor CreateQueryProcessor()
    {
        var tripleStore = new VDS.RDF.TripleStore();
        // Materialise the graph names eagerly to release the QuadStore read lock
        // before calling LoadGraph (which also acquires the same lock).
        var graphNames = ListGraphNames().ToList();
        foreach (var graphName in graphNames)
        {
            var g = new Graph();
            LoadGraph(g, graphName);
            tripleStore.Add(g, mergeIfExists: true);
        }
        return new LeviathanQueryProcessor(new InMemoryDataset(tripleStore, unionDefaultGraph: true));
    }

    private const string XsdStringUri = "http://www.w3.org/2001/XMLSchema#string";

    /// <summary>
    /// Converts a dotNetRDF <see cref="INode"/> to the string representation used by
    /// <see cref="QuadStore"/>:
    /// <list type="bullet">
    ///   <item><description>URI nodes → plain absolute URI string (e.g. <c>http://example.org/</c>)</description></item>
    ///   <item><description>Language-tagged literals → <c>"value"@lang</c></description></item>
    ///   <item><description>Typed literals (non-xsd:string) → <c>"value"^^&lt;datatype&gt;</c></description></item>
    ///   <item><description>Plain literals (or xsd:string) → <c>"value"</c></description></item>
    ///   <item><description>Blank nodes → <c>_:id</c></description></item>
    /// </list>
    /// </summary>
    public static string NodeToString(INode node)
    {
        if (node == null) return string.Empty;
        return node switch
        {
            IUriNode uriNode => uriNode.Uri.AbsoluteUri,
            // Language-tagged literals first (DataType may be rdf:langString in dotNetRDF 3.x)
            ILiteralNode litNode when !string.IsNullOrEmpty(litNode.Language) =>
                $"\"{EscapeLiteral(litNode.Value)}\"@{litNode.Language}",
            // Typed literals with a non-xsd:string datatype (use AbsoluteUri for fragment-aware comparison)
            ILiteralNode litNode when litNode.DataType != null
                && litNode.DataType.AbsoluteUri != XsdStringUri =>
                $"\"{EscapeLiteral(litNode.Value)}\"^^<{litNode.DataType.AbsoluteUri}>",
            // Plain literals (null datatype or xsd:string)
            ILiteralNode litNode => $"\"{EscapeLiteral(litNode.Value)}\"",
            IBlankNode blankNode => $"_:{blankNode.InternalID}",
            _ => node.ToString()
        };
    }

    /// <summary>
    /// Converts a QuadStore string value back to a dotNetRDF <see cref="INode"/>.
    /// Handles blank nodes, angle-bracketed and plain absolute URIs, typed literals,
    /// language-tagged literals, and plain literals.
    /// </summary>
    public static INode StringToNode(string value, INodeFactory factory)
    {
        if (string.IsNullOrEmpty(value))
            return factory.CreateBlankNode();

        // Blank node: _:id
        if (value.StartsWith("_:"))
            return factory.CreateBlankNode(value.Substring(2));

        // Angle-bracketed URI: <http://...>
        if (value.StartsWith("<") && value.EndsWith(">"))
        {
            var uriStr = value.Substring(1, value.Length - 2);
            if (Uri.TryCreate(uriStr, UriKind.Absolute, out var uri))
                return factory.CreateUriNode(uri);
        }

        // Plain absolute URI: http://... or https://... etc.
        if (Uri.TryCreate(value, UriKind.Absolute, out var plainUri))
            return factory.CreateUriNode(plainUri);

        // Quoted literal (plain, typed, or language-tagged)
        if (value.StartsWith("\""))
        {
            // Typed literal: "value"^^<datatype>
            int dtIdx = value.LastIndexOf("\"^^<");
            if (dtIdx > 0)
            {
                var litVal = UnescapeLiteral(value.Substring(1, dtIdx - 1));
                var dtStr = value.Substring(dtIdx + 4, value.Length - dtIdx - 5);
                if (Uri.TryCreate(dtStr, UriKind.Absolute, out var dtUri))
                    return factory.CreateLiteralNode(litVal, dtUri);
            }

            // Language-tagged literal: "value"@lang
            int langIdx = value.LastIndexOf("\"@");
            if (langIdx > 0)
            {
                var litVal = UnescapeLiteral(value.Substring(1, langIdx - 1));
                var lang = value.Substring(langIdx + 2);
                return factory.CreateLiteralNode(litVal, lang);
            }

            // Plain literal: "value"
            if (value.EndsWith("\"") && value.Length >= 2)
                return factory.CreateLiteralNode(UnescapeLiteral(value.Substring(1, value.Length - 2)));
        }

        // Fallback: treat the raw string as a plain literal
        return factory.CreateLiteralNode(value);
    }

    private static string EscapeLiteral(string value) =>
        value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r");

    private static string UnescapeLiteral(string value) =>
        value
            .Replace("\\\"", "\"")
            .Replace("\\n", "\n")
            .Replace("\\r", "\r")
            .Replace("\\\\", "\\");
}
