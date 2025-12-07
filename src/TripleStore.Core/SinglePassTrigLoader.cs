using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace TripleStore.Core;

/// <summary>
/// Loads TriG files directly into a QuadStore in a single pass using ANTLR parser.
/// No intermediate data structures or external RDF libraries required.
/// </summary>
public sealed class SinglePassTrigLoader
{
    private readonly QuadStore _quadStore;

    /// <summary>
    /// Initializes a new instance of the SinglePassTrigLoader class.
    /// </summary>
    /// <param name="quadStore">The QuadStore to load data into.</param>
    /// <exception cref="ArgumentNullException">Thrown when quadStore is null.</exception>
    public SinglePassTrigLoader(QuadStore quadStore)
    {
        _quadStore = quadStore ?? throw new ArgumentNullException(nameof(quadStore));
    }

    /// <summary>
    /// Loads a TriG file from the specified file path in a single pass.
    /// </summary>
    /// <param name="filePath">The path to the TriG file.</param>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    public void LoadFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"TriG file not found: {filePath}", filePath);
        }

        using var stream = File.OpenRead(filePath);
        LoadFromStream(stream);
    }

    /// <summary>
    /// Loads TriG content from a stream in a single pass.
    /// </summary>
    /// <param name="stream">The stream containing TriG data.</param>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    public void LoadFromStream(Stream stream)
    {
        if (stream == null)
        {
            throw new ArgumentNullException(nameof(stream));
        }

        using var reader = new StreamReader(stream);
        LoadFromTextReader(reader);
    }

    /// <summary>
    /// Loads TriG content from a TextReader in a single pass.
    /// Uses ANTLR-generated parser to directly append triples to QuadStore.
    /// </summary>
    /// <param name="reader">The TextReader containing TriG data.</param>
    /// <exception cref="ArgumentNullException">Thrown when reader is null.</exception>
    public void LoadFromTextReader(TextReader reader)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var input = new AntlrInputStream(reader);
        var lexer = new TrigLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new TrigParser(tokens);
        
        var trigDoc = parser.trigDoc();
        var visitor = new TriGVisitorLoader(_quadStore);
        visitor.Visit(trigDoc);
    }

    /// <summary>
    /// Loads TriG content from a string in a single pass.
    /// </summary>
    /// <param name="trigContent">The TriG content as a string.</param>
    /// <exception cref="ArgumentNullException">Thrown when trigContent is null.</exception>
    public void LoadFromString(string trigContent)
    {
        if (trigContent == null)
        {
            throw new ArgumentNullException(nameof(trigContent));
        }

        using var reader = new StringReader(trigContent);
        LoadFromTextReader(reader);
    }

    /// <summary>
    /// Gets statistics about the loaded data.
    /// </summary>
    /// <returns>The count of quads in the store.</returns>
    public int GetLoadedQuadCount()
    {
        return _quadStore.Query().Count();
    }

    /// <summary>
    /// ANTLR visitor that directly appends quads to QuadStore during parsing.
    /// Manages prefixes, base URIs, and current graph context.
    /// </summary>
    private sealed class TriGVisitorLoader : TrigParserBaseVisitor<object?>
    {
        private readonly QuadStore _store;
        private readonly Dictionary<string, string> _prefixes = new();
        private string _baseUri = "http://example.org/base/";
        private string _currentGraph = "urn:x-default:default-graph";
        private int _blankNodeCounter;
        private readonly Dictionary<string, string> _blankNodeMap = new();

        public TriGVisitorLoader(QuadStore store)
        {
            _store = store;
        }

        public override object? VisitTrigDoc(TrigParser.TrigDocContext context)
        {
            // Two-pass: first collect directives (prefixes, base), then process blocks
            if (context.directive() != null && context.directive().Length > 0)
            {
                foreach (var directive in context.directive())
                {
                    Visit(directive);
                }
            }

            // Now process blocks
            if (context.block() != null && context.block().Length > 0)
            {
                foreach (var block in context.block())
                {
                    Visit(block);
                }
            }

            return null;
        }

        public override object? VisitDirective(TrigParser.DirectiveContext context)
        {
            if (context.prefixID() != null)
            {
                var prefixCtx = context.prefixID();
                var prefixLabel = prefixCtx.PNAME_NS().GetText().TrimEnd(':');
                var iriRef = ExtractIriRef(prefixCtx.IRIREF());
                _prefixes[prefixLabel] = iriRef;
            }
            else if (context.@base() != null)
            {
                var baseCtx = context.@base();
                _baseUri = ExtractIriRef(baseCtx.IRIREF());
            }
            else if (context.sparqlPrefix() != null)
            {
                var sparqlCtx = context.sparqlPrefix();
                var prefixLabel = sparqlCtx.PNAME_NS().GetText().TrimEnd(':');
                var iriRef = ExtractIriRef(sparqlCtx.IRIREF());
                _prefixes[prefixLabel] = iriRef;
            }
            else if (context.sparqlBase() != null)
            {
                var sparqlBaseCtx = context.sparqlBase();
                _baseUri = ExtractIriRef(sparqlBaseCtx.IRIREF());
            }

            return null;
        }

        public override object? VisitBlock(TrigParser.BlockContext context)
        {
            if (context.triplesOrGraph() != null)
            {
                Visit(context.triplesOrGraph());
            }
            else if (context.wrappedGraph() != null)
            {
                Visit(context.wrappedGraph());
            }
            else if (context.triples2() != null)
            {
                Visit(context.triples2());
            }
            else if (context.GRAPH() != null)
            {
                // GRAPH keyword: labelOrSubject wrappedGraph
                var labelOrSubject = context.labelOrSubject();
                var graphUri = ExtractIri(labelOrSubject);
                var prevGraph = _currentGraph;
                _currentGraph = graphUri;
                
                Visit(context.wrappedGraph());
                
                _currentGraph = prevGraph;
            }

            return null;
        }

        public override object? VisitTriplesOrGraph(TrigParser.TriplesOrGraphContext context)
        {
            var labelOrSubject = context.labelOrSubject();
            
            if (context.wrappedGraph() != null)
            {
                // Named graph: labelOrSubject wrappedGraph
                var graphUri = ExtractIri(labelOrSubject);
                var prevGraph = _currentGraph;
                _currentGraph = graphUri;
                
                Visit(context.wrappedGraph());
                
                _currentGraph = prevGraph;
            }
            else if (context.predicateObjectList() != null)
            {
                // Triples: subject predicateObjectList
                var subject = ExtractIri(labelOrSubject);
                VisitPredicateObjectList(subject, context.predicateObjectList());
            }

            if (context.reifiedTriple() != null)
            {
                Visit(context.reifiedTriple());
            }

            return null;
        }

        public override object? VisitWrappedGraph(TrigParser.WrappedGraphContext context)
        {
            if (context.triplesBlock() != null)
            {
                Visit(context.triplesBlock());
            }

            return null;
        }

        public override object? VisitTriplesBlock(TrigParser.TriplesBlockContext context)
        {
            // Visit all triples in the block
            for (int i = 0; i < context.ChildCount; i++)
            {
                var child = context.GetChild(i);
                if (child is TrigParser.TriplesContext triplesCtx)
                {
                    Visit(triplesCtx);
                }
                else if (child is TrigParser.Triples2Context triples2Ctx)
                {
                    Visit(triples2Ctx);
                }
            }

            return null;
        }

        public override object? VisitTriples(TrigParser.TriplesContext context)
        {
            var subject = ExtractSubject(context.subject());
            VisitPredicateObjectList(subject, context.predicateObjectList());
            return null;
        }

        public override object? VisitTriples2(TrigParser.Triples2Context context)
        {
            string subject;

            if (context.blankNodePropertyList() != null)
            {
                subject = GenerateBlankNode();
                var predicateObjectList = context.blankNodePropertyList().predicateObjectList();
                if (predicateObjectList != null)
                {
                    VisitPredicateObjectList(subject, predicateObjectList);
                }
            }
            else if (context.collection() != null)
            {
                subject = ProcessCollection(context.collection());
            }
            else
            {
                return null;
            }

            if (context.predicateObjectList() != null)
            {
                VisitPredicateObjectList(subject, context.predicateObjectList());
            }

            return null;
        }

        private void VisitPredicateObjectList(string subject, TrigParser.PredicateObjectListContext context)
        {
            // Extract all verb/objectList pairs and emit quads
            var verbs = new List<string>();
            var objectLists = new List<TrigParser.ObjectListContext>();

            for (int i = 0; i < context.ChildCount; i++)
            {
                var child = context.GetChild(i);
                if (child is TrigParser.VerbContext verbCtx)
                {
                    verbs.Add(ExtractVerb(verbCtx));
                }
                else if (child is TrigParser.ObjectListContext objListCtx)
                {
                    objectLists.Add(objListCtx);
                }
            }

            // Emit quads for each verb/objectList pair
            for (int i = 0; i < verbs.Count && i < objectLists.Count; i++)
            {
                var verb = verbs[i];
                var objList = objectLists[i];

                foreach (var obj in objList.@object())
                {
                    var objValue = ExtractObject(obj);
                    AppendQuad(subject, verb, objValue);
                }
            }
        }

        private string ExtractSubject(TrigParser.SubjectContext context)
        {
            if (context.iri() != null)
                return ExtractIri(context.iri());
            if (context.BlankNode() != null)
                return ExtractBlankNodeTerminal(context.BlankNode());
            if (context.collection() != null)
                return ProcessCollection(context.collection());

            throw new InvalidOperationException("Unknown subject type");
        }

        private string ExtractObject(TrigParser.ObjectContext context)
        {
            if (context.iri() != null)
                return ExtractIri(context.iri());
            if (context.BlankNode() != null)
                return ExtractBlankNodeTerminal(context.BlankNode());
            if (context.literal() != null)
                return ExtractLiteral(context.literal());
            if (context.collection() != null)
                return ProcessCollection(context.collection());
            if (context.blankNodePropertyList() != null)
                return ProcessBlankNodePropertyList(context.blankNodePropertyList());
            if (context.tripleTerm() != null)
                return ProcessTripleTerm(context.tripleTerm());
            if (context.reifiedTriple() != null)
                return ProcessReifiedTriple(context.reifiedTriple());

            throw new InvalidOperationException("Unknown object type");
        }

        private string ExtractVerb(TrigParser.VerbContext context)
        {
            if (context.predicate() != null)
                return ExtractIri(context.predicate().iri());
            if (context.A() != null)
                return "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";

            throw new InvalidOperationException("Unknown verb type");
        }

        private string ExtractIri(TrigParser.IriContext context)
        {
            if (context.IRIREF() != null)
                return ExtractIriRef(context.IRIREF());
            if (context.prefixedName() != null)
                return ExpandPrefixedName(context.prefixedName());

            throw new InvalidOperationException("Unknown IRI type");
        }

        private string ExtractIri(TrigParser.LabelOrSubjectContext context)
        {
            if (context.iri() != null)
                return ExtractIri(context.iri());
            if (context.blankNode() != null)
                return ExtractBlankNode(context.blankNode());

            throw new InvalidOperationException("Unknown label/subject type");
        }

        private string ExtractIriRef(ITerminalNode node)
        {
            var text = node.GetText();
            // Remove angle brackets and unescape
            return UnescapeIri(text.Substring(1, text.Length - 2));
        }

        private string ExpandPrefixedName(TrigParser.PrefixedNameContext context)
        {
            var text = context.GetText();

            if (text == "a")
                return "http://www.w3.org/1999/02/22-rdf-syntax-ns#type";

            if (text.Contains(':'))
            {
                var colonIdx = text.IndexOf(':');
                var prefix = text.Substring(0, colonIdx);
                var localName = text.Substring(colonIdx + 1);

                if (_prefixes.TryGetValue(prefix, out var baseIri))
                {
                    return baseIri + localName;
                }
                else if (prefix == "")
                {
                    // Default namespace
                    return _baseUri.TrimEnd('/') + "/" + localName;
                }
            }

            // Treat as relative IRI
            return ResolveRelativeIri(text);
        }

        private string ResolveRelativeIri(string relative)
        {
            if (relative.StartsWith("http://") || relative.StartsWith("https://") || relative.Contains("://"))
                return relative;

            return _baseUri.TrimEnd('/') + "/" + relative;
        }

        private string ExtractBlankNode(TrigParser.BlankNodeContext context)
        {
            if (context.BLANK_NODE_LABEL() != null)
            {
                var label = context.BLANK_NODE_LABEL().GetText();
                if (!_blankNodeMap.ContainsKey(label))
                {
                    _blankNodeMap[label] = label;
                }
                return _blankNodeMap[label];
            }
            if (context.ANON() != null)
            {
                return GenerateBlankNode();
            }

            throw new InvalidOperationException("Unknown blank node type");
        }

        private string ExtractBlankNodeTerminal(ITerminalNode node)
        {
            // Handle the BlankNode TOKEN which could be _:label or []
            var text = node.GetText();
            
            if (text == "[]")
            {
                return GenerateBlankNode();
            }

            if (!_blankNodeMap.ContainsKey(text))
            {
                _blankNodeMap[text] = text;
            }
            return _blankNodeMap[text];
        }

        private string ProcessBlankNodePropertyList(TrigParser.BlankNodePropertyListContext context)
        {
            var blankNode = GenerateBlankNode();
            var predicateObjectList = context.predicateObjectList();
            if (predicateObjectList != null)
            {
                VisitPredicateObjectList(blankNode, predicateObjectList);
            }
            return blankNode;
        }

        private string ProcessReifiedTriple(TrigParser.ReifiedTripleContext context)
        {
            // For RDF-star reified triples, create a blank node
            // This is a simplified implementation
            return GenerateBlankNode();
        }

        private string ExtractLiteral(TrigParser.LiteralContext context)
        {
            if (context.rdfLiteral() != null)
                return ExtractRdfLiteral(context.rdfLiteral());
            if (context.numericLiteral() != null)
                return ExtractNumericLiteral(context.numericLiteral());
            if (context.booleanLiteral() != null)
                return ExtractBooleanLiteral(context.booleanLiteral());

            throw new InvalidOperationException("Unknown literal type");
        }

        private string ExtractRdfLiteral(TrigParser.RdfLiteralContext context)
        {
            var stringValue = ParseStringLiteral(context.string_());
            var lang = context.LANG_DIR();
            var datatypeIri = context.iri();

            if (lang != null)
            {
                var langTag = lang.GetText().Substring(1); // Remove @
                return $"{stringValue}@{langTag}";
            }

            if (datatypeIri != null)
            {
                var datatype = ExtractIri(datatypeIri);
                return $"{stringValue}^^<{datatype}>";
            }

            // Plain literal
            return stringValue;
        }

        private string ExtractNumericLiteral(TrigParser.NumericLiteralContext context)
        {
            string value;
            string xsdType;

            if (context.INTEGER() != null)
            {
                value = context.INTEGER().GetText();
                xsdType = "integer";
            }
            else if (context.DECIMAL() != null)
            {
                value = context.DECIMAL().GetText();
                xsdType = "decimal";
            }
            else if (context.DOUBLE() != null)
            {
                value = context.DOUBLE().GetText();
                xsdType = "double";
            }
            else
            {
                throw new InvalidOperationException("Unknown numeric literal");
            }

            return $"\"{value}\"^^<http://www.w3.org/2001/XMLSchema#{xsdType}>";
        }

        private string ExtractBooleanLiteral(TrigParser.BooleanLiteralContext context)
        {
            var value = context.TRUE() != null ? "true" : "false";
            return $"\"{value}\"^^<http://www.w3.org/2001/XMLSchema#boolean>";
        }

        private string ParseStringLiteral(TrigParser.String_Context ctx)
        {
            var rawText = ctx.GetText();

            // Strip outer quotes
            if (rawText.StartsWith("\"\"\""))
                rawText = rawText.Substring(3, rawText.Length - 6);
            else if (rawText.StartsWith("'''"))
                rawText = rawText.Substring(3, rawText.Length - 6);
            else
                rawText = rawText.Substring(1, rawText.Length - 2);

            // Unescape
            return UnescapeString(rawText);
        }

        private string UnescapeString(string text)
        {
            var result = new System.Text.StringBuilder();
            int i = 0;

            while (i < text.Length)
            {
                if (text[i] == '\\' && i + 1 < text.Length)
                {
                    switch (text[i + 1])
                    {
                        case 't':
                            result.Append('\t');
                            i += 2;
                            break;
                        case 'n':
                            result.Append('\n');
                            i += 2;
                            break;
                        case 'r':
                            result.Append('\r');
                            i += 2;
                            break;
                        case '\\':
                            result.Append('\\');
                            i += 2;
                            break;
                        case '"':
                            result.Append('"');
                            i += 2;
                            break;
                        case '\'':
                            result.Append('\'');
                            i += 2;
                            break;
                        case 'u':
                            if (i + 5 < text.Length)
                            {
                                var hexCode = text.Substring(i + 2, 4);
                                if (int.TryParse(hexCode, System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
                                {
                                    result.Append((char)codePoint);
                                    i += 6;
                                }
                                else
                                {
                                    result.Append(text[i]);
                                    i++;
                                }
                            }
                            else
                            {
                                result.Append(text[i]);
                                i++;
                            }
                            break;
                        case 'U':
                            if (i + 9 < text.Length)
                            {
                                var hexCode = text.Substring(i + 2, 8);
                                if (int.TryParse(hexCode, System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
                                {
                                    result.Append(char.ConvertFromUtf32(codePoint));
                                    i += 10;
                                }
                                else
                                {
                                    result.Append(text[i]);
                                    i++;
                                }
                            }
                            else
                            {
                                result.Append(text[i]);
                                i++;
                            }
                            break;
                        default:
                            result.Append(text[i]);
                            i++;
                            break;
                    }
                }
                else
                {
                    result.Append(text[i]);
                    i++;
                }
            }

            return result.ToString();
        }

        private string UnescapeIri(string text)
        {
            // IRIs can have \uXXXX escapes
            var result = new System.Text.StringBuilder();
            int i = 0;

            while (i < text.Length)
            {
                if (text[i] == '\\' && i + 1 < text.Length)
                {
                    if (text[i + 1] == 'u' && i + 5 < text.Length)
                    {
                        var hexCode = text.Substring(i + 2, 4);
                        if (int.TryParse(hexCode, System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
                        {
                            result.Append((char)codePoint);
                            i += 6;
                        }
                        else
                        {
                            result.Append(text[i]);
                            i++;
                        }
                    }
                    else if (text[i + 1] == 'U' && i + 9 < text.Length)
                    {
                        var hexCode = text.Substring(i + 2, 8);
                        if (int.TryParse(hexCode, System.Globalization.NumberStyles.HexNumber, null, out var codePoint))
                        {
                            result.Append(char.ConvertFromUtf32(codePoint));
                            i += 10;
                        }
                        else
                        {
                            result.Append(text[i]);
                            i++;
                        }
                    }
                    else
                    {
                        result.Append(text[i]);
                        i++;
                    }
                }
                else
                {
                    result.Append(text[i]);
                    i++;
                }
            }

            return result.ToString();
        }

        private string ProcessCollection(TrigParser.CollectionContext context)
        {
            var objects = context.@object();
            
            if (objects == null || objects.Length == 0)
            {
                // Empty collection → rdf:nil
                return "http://www.w3.org/1999/02/22-rdf-syntax-ns#nil";
            }

            // Create linked list
            string listHead = $"_:list{++_blankNodeCounter}";
            string currentNode = listHead;

            for (int i = 0; i < objects.Length; i++)
            {
                var item = ExtractObject(objects[i]);
                AppendQuad(currentNode, "http://www.w3.org/1999/02/22-rdf-syntax-ns#first", item);

                if (i < objects.Length - 1)
                {
                    string nextNode = $"_:list{++_blankNodeCounter}";
                    AppendQuad(currentNode, "http://www.w3.org/1999/02/22-rdf-syntax-ns#rest", nextNode);
                    currentNode = nextNode;
                }
                else
                {
                    AppendQuad(currentNode, "http://www.w3.org/1999/02/22-rdf-syntax-ns#rest",
                        "http://www.w3.org/1999/02/22-rdf-syntax-ns#nil");
                }
            }

            return listHead;
        }

        private string ProcessTripleTerm(TrigParser.TripleTermContext context)
        {
            // RDF-star reified triples: for now, generate blank node
            return GenerateBlankNode();
        }

        private string GenerateBlankNode()
        {
            return $"_:b{++_blankNodeCounter}";
        }

        private void AppendQuad(string subject, string predicate, string obj)
        {
            _store.Append(subject, predicate, obj, _currentGraph);
        }
    }
}
