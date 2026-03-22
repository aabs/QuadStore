using System;
using System.Collections.Generic;
using System.Linq;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Query.Patterns;
using VDS.RDF.Storage;
using VDS.RDF.Storage.Management;
using VDS.RDF.Update;
using VDS.RDF.Update.Commands;

namespace TripleStore.Core;

/// <summary>
/// Adapter that exposes <see cref="QuadStore"/> as a dotNetRDF <see cref="IStorageProvider"/>,
/// enabling compatibility with the Leviathan SPARQL query engine and other dotNetRDF tooling.
/// </summary>
/// <remarks>
/// <para>This implementation maps dotNetRDF storage operations to QuadStore's underlying
/// columnar bitmap store. Quad support is enabled: named graphs are surfaced as required
/// by dotNetRDF.</para>
/// <para><b>Supported operations:</b></para>
/// <list type="bullet">
///   <item><description>Graph deletion via <see cref="DeleteGraph(Uri)"/> and
///   <see cref="DeleteGraph(string)"/> using tombstone-based soft delete.</description></item>
///   <item><description>Triple removal via
///   <see cref="UpdateGraph(IRefNode, IEnumerable{Triple}, IEnumerable{Triple})"/> with both
///   additions and removals (removals processed before additions).</description></item>
///   <item><description>SPARQL 1.1 Update via <see cref="Update"/>: INSERT DATA, DELETE DATA,
///   DELETE/INSERT WHERE, DROP, and CLEAR commands.</description></item>
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
        IOBehaviour.CanUpdateAddTriples |
        IOBehaviour.CanUpdateDeleteTriples;

    /// <inheritdoc/>
    public bool UpdateSupported => true;

    /// <summary>
    /// Returns <see langword="true"/>. QuadStore supports graph deletion via tombstone-based soft delete.
    /// </summary>
    public bool DeleteSupported => true;

    /// <inheritdoc/>
    public bool ListGraphsSupported => true;

    // ── IStorageProvider ────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="null"/>. QuadStore is a standalone store with no parent server.
    /// </summary>
    public IStorageServer? ParentServer => null;

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
        var normalised = graphUri != null ? NormaliseGraphUri(graphUri) : null;
        if (normalised != null && Uri.TryCreate(normalised, UriKind.Absolute, out var uri))
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
    /// <remarks>
    /// Both additions and removals are supported. Removals are processed before additions,
    /// following SPARQL Update semantics. Non-existent removal triples are silently skipped.
    /// </remarks>
    public void UpdateGraph(IRefNode graphName, IEnumerable<Triple> additions, IEnumerable<Triple> removals)
    {
        string graphUri = graphName is IUriNode un ? un.Uri.AbsoluteUri : string.Empty;
        UpdateGraphInternal(graphUri, additions, removals);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Both additions and removals are supported. Removals are processed before additions,
    /// following SPARQL Update semantics. Non-existent removal triples are silently skipped.
    /// </remarks>
    public void UpdateGraph(Uri graphUri, IEnumerable<Triple> additions, IEnumerable<Triple> removals)
    {
        UpdateGraphInternal(graphUri?.AbsoluteUri ?? string.Empty, additions, removals);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Both additions and removals are supported. Removals are processed before additions,
    /// following SPARQL Update semantics. Non-existent removal triples are silently skipped.
    /// </remarks>
    public void UpdateGraph(string graphUri, IEnumerable<Triple> additions, IEnumerable<Triple> removals)
    {
        UpdateGraphInternal(graphUri ?? string.Empty, additions, removals);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Deletes all quads in the specified named graph. If <paramref name="graphUri"/> is
    /// <see langword="null"/>, deletes quads in the default graph.
    /// </remarks>
    public void DeleteGraph(Uri graphUri)
    {
        DeleteGraphInternal(graphUri?.AbsoluteUri ?? string.Empty);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Deletes all quads in the specified named graph. If <paramref name="graphUri"/> is
    /// <see langword="null"/> or empty, deletes quads in the default graph.
    /// </remarks>
    public void DeleteGraph(string graphUri)
    {
        DeleteGraphInternal(graphUri);
    }

    /// <inheritdoc/>
    public IEnumerable<Uri> ListGraphs()
    {
        return _store.Query()
            .Select(q => NormaliseGraphUri(q.graph))
            .Distinct()
            .Where(g => Uri.TryCreate(g, UriKind.Absolute, out _))
            .Select(g => new Uri(g));
    }

    /// <inheritdoc/>
    public IEnumerable<string> ListGraphNames()
    {
        return _store.Query()
            .Select(q => NormaliseGraphUri(q.graph))
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
    /// Executes a SPARQL Update command string against the store.
    /// Supports INSERT DATA, DELETE DATA, DELETE/INSERT WHERE, DROP, and CLEAR commands.
    /// </summary>
    /// <param name="sparqlUpdate">The SPARQL Update command string.</param>
    /// <exception cref="RdfStorageException">
    /// Thrown when the command string contains invalid syntax or an unsupported command type.
    /// </exception>
    public void Update(string sparqlUpdate)
    {
        SparqlUpdateCommandSet commandSet;
        try
        {
            var parser = new SparqlUpdateParser();
            commandSet = parser.ParseFromString(sparqlUpdate);
        }
        catch (Exception ex)
        {
            throw new RdfStorageException("Failed to parse SPARQL Update: " + ex.Message, ex);
        }

        for (var i = 0; i < commandSet.CommandCount; i++)
        {
            var cmd = commandSet[i];
            switch (cmd)
            {
                case InsertDataCommand insertData:
                    ProcessInsertData(insertData);
                    break;
                case DeleteDataCommand deleteData:
                    ProcessDeleteData(deleteData);
                    break;
                case ModifyCommand modify:
                    ProcessModify(modify);
                    break;
                case DeleteCommand delete:
                    ProcessDeleteWhere(delete);
                    break;
                case InsertCommand insert:
                    ProcessInsertWhere(insert);
                    break;
                case DropCommand drop:
                    ProcessDropOrClear(drop.Mode, drop.TargetGraphName, drop.Silent);
                    break;
                case ClearCommand clear:
                    ProcessDropOrClear(clear.Mode, clear.TargetGraphName, clear.Silent);
                    break;
                default:
                    throw new RdfStorageException(
                        $"Unsupported SPARQL Update command type: {cmd.CommandType}. " +
                        "Supported commands: INSERT DATA, DELETE DATA, DELETE/INSERT WHERE, DROP, CLEAR.");
            }
        }
    }

    // ── IDisposable ─────────────────────────────────────────────────────────

    /// <summary>
    /// Disposes this adapter.
    /// The lifecycle of the underlying <see cref="QuadStore"/> is managed by the caller.
    /// </summary>
    public void Dispose()
    {
        // QuadStore lifecycle is managed externally; nothing to dispose here.
    }

    // ── SPARQL Update helpers ──────────────────────────────────────────────

    private void ProcessInsertData(InsertDataCommand cmd)
    {
        var pattern = cmd.DataPattern;

        if (pattern.IsGraph)
        {
            // Single GRAPH clause: DataPattern itself is the graph pattern
            var graphUri = pattern.GraphSpecifier.Value;
            foreach (var tp in pattern.TriplePatterns.OfType<IConstructTriplePattern>())
            {
                AppendTriplePattern(tp, graphUri);
            }
            return;
        }

        // Default graph triples (no GRAPH clause)
        foreach (var tp in pattern.TriplePatterns.OfType<IConstructTriplePattern>())
        {
            AppendTriplePattern(tp, string.Empty);
        }

        // GRAPH-scoped triples (mixed default + GRAPH clauses)
        foreach (var childPattern in pattern.ChildGraphPatterns)
        {
            if (!childPattern.IsGraph) continue;
            var graphUri = childPattern.GraphSpecifier.Value;
            foreach (var tp in childPattern.TriplePatterns.OfType<IConstructTriplePattern>())
            {
                AppendTriplePattern(tp, graphUri);
            }
        }
    }

    private void ProcessDeleteData(DeleteDataCommand cmd)
    {
        var pattern = cmd.DataPattern;

        if (pattern.IsGraph)
        {
            // Single GRAPH clause: DataPattern itself is the graph pattern
            var graphUri = pattern.GraphSpecifier.Value;
            foreach (var tp in pattern.TriplePatterns.OfType<IConstructTriplePattern>())
            {
                DeleteTriplePattern(tp, graphUri);
            }
            return;
        }

        // Default graph triples (no GRAPH clause)
        foreach (var tp in pattern.TriplePatterns.OfType<IConstructTriplePattern>())
        {
            DeleteTriplePattern(tp, string.Empty);
        }

        // GRAPH-scoped triples (mixed default + GRAPH clauses)
        foreach (var childPattern in pattern.ChildGraphPatterns)
        {
            if (!childPattern.IsGraph) continue;
            var graphUri = childPattern.GraphSpecifier.Value;
            foreach (var tp in childPattern.TriplePatterns.OfType<IConstructTriplePattern>())
            {
                DeleteTriplePattern(tp, graphUri);
            }
        }
    }

    private void ProcessModify(ModifyCommand cmd)
    {
        // 1. Build a snapshot dataset and evaluate the WHERE pattern to collect bindings
        var processor = CreateQueryProcessor();
        var query = new SparqlQueryParser().ParseFromString("SELECT * WHERE " + cmd.WherePattern.ToString());
        var result = processor.ProcessQuery(query);

        if (result is not SparqlResultSet resultSet || resultSet.Count == 0)
            return;

        // 2. Snapshot: collect all bindings before applying any changes
        var bindings = resultSet.Results.ToList();

        // 3. Determine the target graph URI (from WITH clause, if any)
        string targetGraph = cmd.TargetGraph is IUriNode uriNode
            ? uriNode.Uri.AbsoluteUri
            : string.Empty;

        // 4. Apply DELETE template: for each binding, instantiate and delete
        foreach (var binding in bindings)
        {
            InstantiateAndDelete(cmd.DeletePattern, binding, targetGraph);
        }

        // 5. Apply INSERT template: for each binding, instantiate and insert
        foreach (var binding in bindings)
        {
            InstantiateAndInsert(cmd.InsertPattern, binding, targetGraph);
        }
    }

    private void ProcessDeleteWhere(DeleteCommand cmd)
    {
        var processor = CreateQueryProcessor();
        var query = new SparqlQueryParser().ParseFromString("SELECT * WHERE " + cmd.WherePattern.ToString());
        var result = processor.ProcessQuery(query);

        if (result is not SparqlResultSet resultSet || resultSet.Count == 0)
            return;

        var bindings = resultSet.Results.ToList();

        string targetGraph = cmd.TargetGraph is IUriNode uriNode
            ? uriNode.Uri.AbsoluteUri
            : string.Empty;

        foreach (var binding in bindings)
        {
            InstantiateAndDelete(cmd.DeletePattern, binding, targetGraph);
        }
    }

    private void ProcessInsertWhere(InsertCommand cmd)
    {
        var processor = CreateQueryProcessor();
        var query = new SparqlQueryParser().ParseFromString("SELECT * WHERE " + cmd.WherePattern.ToString());
        var result = processor.ProcessQuery(query);

        if (result is not SparqlResultSet resultSet || resultSet.Count == 0)
            return;

        var bindings = resultSet.Results.ToList();

        string targetGraph = cmd.TargetGraph is IUriNode uriNode
            ? uriNode.Uri.AbsoluteUri
            : string.Empty;

        foreach (var binding in bindings)
        {
            InstantiateAndInsert(cmd.InsertPattern, binding, targetGraph);
        }
    }

    private void ProcessDropOrClear(ClearMode mode, IRefNode targetGraphName, bool silent)
    {
        switch (mode)
        {
            case ClearMode.Graph:
                var uri = targetGraphName is IUriNode uriNode
                    ? uriNode.Uri.AbsoluteUri
                    : string.Empty;
                if (!silent)
                {
                    var graphNames = ListGraphNames();
                    if (!graphNames.Contains(uri))
                    {
                        throw new RdfStorageException(
                            $"Graph <{uri}> does not exist in the store.");
                    }
                }
                _store.Delete(graph: uri);
                break;

            case ClearMode.All:
                _store.Delete();
                break;

            case ClearMode.Default:
                _store.Delete(graph: "");
                break;

            case ClearMode.Named:
                foreach (var graphName in ListGraphNames().ToList())
                {
                    if (!string.IsNullOrEmpty(graphName))
                    {
                        _store.Delete(graph: graphName);
                    }
                }
                break;
        }
    }

    private void InstantiateAndDelete(GraphPattern pattern, ISparqlResult binding, string defaultGraph)
    {
        foreach (var tp in pattern.TriplePatterns.OfType<IConstructTriplePattern>())
        {
            var triplePattern = (TriplePattern)tp;
            var s = ResolvePatternItem(triplePattern.Subject, binding);
            var p = ResolvePatternItem(triplePattern.Predicate, binding);
            var o = ResolvePatternItem(triplePattern.Object, binding);
            if (s != null && p != null && o != null)
            {
                _store.Delete(subject: s, predicate: p, obj: o, graph: defaultGraph);
            }
        }

        foreach (var childPattern in pattern.ChildGraphPatterns)
        {
            if (!childPattern.IsGraph) continue;
            var graphUri = childPattern.GraphSpecifier.Value;
            foreach (var tp in childPattern.TriplePatterns.OfType<IConstructTriplePattern>())
            {
                var triplePattern = (TriplePattern)tp;
                var s = ResolvePatternItem(triplePattern.Subject, binding);
                var p = ResolvePatternItem(triplePattern.Predicate, binding);
                var o = ResolvePatternItem(triplePattern.Object, binding);
                if (s != null && p != null && o != null)
                {
                    _store.Delete(subject: s, predicate: p, obj: o, graph: graphUri);
                }
            }
        }
    }

    private void InstantiateAndInsert(GraphPattern pattern, ISparqlResult binding, string defaultGraph)
    {
        foreach (var tp in pattern.TriplePatterns.OfType<IConstructTriplePattern>())
        {
            var triplePattern = (TriplePattern)tp;
            var s = ResolvePatternItem(triplePattern.Subject, binding);
            var p = ResolvePatternItem(triplePattern.Predicate, binding);
            var o = ResolvePatternItem(triplePattern.Object, binding);
            if (s != null && p != null && o != null)
            {
                _store.Append(s, p, o, defaultGraph);
            }
        }

        foreach (var childPattern in pattern.ChildGraphPatterns)
        {
            if (!childPattern.IsGraph) continue;
            var graphUri = childPattern.GraphSpecifier.Value;
            foreach (var tp in childPattern.TriplePatterns.OfType<IConstructTriplePattern>())
            {
                var triplePattern = (TriplePattern)tp;
                var s = ResolvePatternItem(triplePattern.Subject, binding);
                var p = ResolvePatternItem(triplePattern.Predicate, binding);
                var o = ResolvePatternItem(triplePattern.Object, binding);
                if (s != null && p != null && o != null)
                {
                    _store.Append(s, p, o, graphUri);
                }
            }
        }
    }

    private static string ResolvePatternItem(PatternItem item, ISparqlResult binding)
    {
        if (item is NodeMatchPattern nodeMatch)
        {
            return NodeToString(nodeMatch.Node);
        }

        if (item is VariablePattern varPattern)
        {
            var varName = varPattern.VariableName;
            if (binding.HasValue(varName))
            {
                var node = binding[varName];
                if (node != null)
                {
                    return NodeToString(node);
                }
            }
            return null;
        }

        return item.ToString();
    }

    private void AppendTriplePattern(IConstructTriplePattern tp, string graphUri)
    {
        var triplePattern = (TriplePattern)tp;
        var s = PatternItemToString(triplePattern.Subject);
        var p = PatternItemToString(triplePattern.Predicate);
        var o = PatternItemToString(triplePattern.Object);
        _store.Append(s, p, o, graphUri);
    }

    private void DeleteTriplePattern(IConstructTriplePattern tp, string graphUri)
    {
        var triplePattern = (TriplePattern)tp;
        var s = PatternItemToString(triplePattern.Subject);
        var p = PatternItemToString(triplePattern.Predicate);
        var o = PatternItemToString(triplePattern.Object);
        _store.Delete(subject: s, predicate: p, obj: o, graph: graphUri);
    }

    private static string PatternItemToString(PatternItem item)
    {
        if (item is NodeMatchPattern nodeMatch)
        {
            return NodeToString(nodeMatch.Node);
        }
        return item.ToString();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Deletes all quads in the specified graph, trying both the plain and angle-bracketed
    /// forms of the URI to handle data stored in either format.
    /// </summary>
    private void DeleteGraphInternal(string graphUri)
    {
        if (string.IsNullOrEmpty(graphUri))
        {
            _store.Delete(graph: "");
            return;
        }

        var normalised = NormaliseGraphUri(graphUri);
        _store.Delete(graph: normalised);
        _store.Delete(graph: "<" + normalised + ">");
    }

    private void LoadGraphInternal(IGraph g, string? graphUri)
    {
        foreach (var (s, p, o, _) in QueryByGraph(graphUri))
        {
            g.Assert(new Triple(
                StringToNode(s, g),
                StringToNode(p, g),
                StringToNode(o, g)));
        }
    }

    private void LoadGraphHandlerInternal(IRdfHandler handler, string? graphUri)
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
        // Process removals before additions (SPARQL Update semantics)
        if (removals != null)
        {
            foreach (var triple in removals)
            {
                _store.Delete(
                    subject: NodeToString(triple.Subject),
                    predicate: NodeToString(triple.Predicate),
                    obj: NodeToString(triple.Object),
                    graph: graphUri);
            }
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
    private IEnumerable<(string s, string p, string o, string g)> QueryByGraph(string? graphUri)
    {
        if (graphUri == null)
            return _store.Query();

        // Normalise: derive both the plain and angle-bracketed form of the URI.
        string plain = NormaliseGraphUri(graphUri);
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

    /// <summary>
    /// Builds a snapshot <see cref="LeviathanQueryProcessor"/> by loading every named graph
    /// from the QuadStore into a fresh in-memory dataset.
    /// </summary>
    /// <remarks>
    /// <b>Performance note:</b> this method creates a full in-memory copy of the store on
    /// every call. For large stores or high query rates, consider using a purpose-built
    /// in-memory RDF store (e.g. <see cref="VDS.RDF.TripleStore"/> populated once) and
    /// reloading only on writes.
    /// </remarks>
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

    /// <summary>
    /// Strips surrounding angle brackets from a stored graph URI string, returning the
    /// plain absolute URI.  If the value is not angle-bracketed it is returned unchanged.
    /// </summary>
    private static string NormaliseGraphUri(string graphUri) =>
        graphUri.StartsWith("<") && graphUri.EndsWith(">") ? graphUri[1..^1] : graphUri;

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
            return factory.CreateBlankNode(value[2..]);

        // Angle-bracketed URI: <http://...>
        if (value.StartsWith("<") && value.EndsWith(">"))
        {
            var uriStr = value[1..^1];
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
                var litVal = UnescapeLiteral(value[1..dtIdx]);
                var dtStr = value[(dtIdx + 4)..^1];
                if (Uri.TryCreate(dtStr, UriKind.Absolute, out var dtUri))
                    return factory.CreateLiteralNode(litVal, dtUri);
            }

            // Language-tagged literal: "value"@lang
            int langIdx = value.LastIndexOf("\"@");
            if (langIdx > 0)
            {
                var litVal = UnescapeLiteral(value[1..langIdx]);
                var lang = value[(langIdx + 2)..];
                return factory.CreateLiteralNode(litVal, lang);
            }

            // Plain literal: "value"
            if (value.EndsWith("\"") && value.Length >= 2)
                return factory.CreateLiteralNode(UnescapeLiteral(value[1..^1]));
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

    private static string UnescapeLiteral(string value)
    {
        // Process escape sequences token-by-token to avoid chained Replace corruption.
        // e.g. "\\n" must become backslash+n, not a newline.
        var sb = new System.Text.StringBuilder(value.Length);
        int i = 0;
        while (i < value.Length)
        {
            if (value[i] == '\\' && i + 1 < value.Length)
            {
                switch (value[i + 1])
                {
                    case '\\': sb.Append('\\'); i += 2; break;
                    case '"':  sb.Append('"');  i += 2; break;
                    case 'n':  sb.Append('\n'); i += 2; break;
                    case 'r':  sb.Append('\r'); i += 2; break;
                    default:
                        // Unknown escape sequence: pass through the backslash and let the
                        // next iteration handle the following character (e.g. \x → \x).
                        sb.Append(value[i]);
                        i++;
                        break;
                }
            }
            else
            {
                sb.Append(value[i]);
                i++;
            }
        }
        return sb.ToString();
    }
}
