using System;
using System.IO;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace TripleStore.Core;

/// <summary>
/// Loads TriG files directly into a QuadStore.
/// Uses dotNetRDF's parser to load TriG and transfers data directly to QuadStore via streaming.
/// </summary>
public sealed class TriGLoader
{
    private readonly QuadStore _quadStore;

    /// <summary>
    /// Initializes a new instance of the TriGLoader class.
    /// </summary>
    /// <param name="quadStore">The QuadStore to load data into.</param>
    /// <exception cref="ArgumentNullException">Thrown when quadStore is null.</exception>
    public TriGLoader(QuadStore quadStore)
    {
        _quadStore = quadStore ?? throw new ArgumentNullException(nameof(quadStore));
    }

    /// <summary>
    /// Loads a TriG file from the specified file path.
    /// </summary>
    /// <param name="filePath">The path to the TriG file.</param>
    /// <exception cref="ArgumentNullException">Thrown when filePath is null or whitespace.</exception>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
    /// <exception cref="RdfParseException">Thrown when the file cannot be parsed.</exception>
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
    /// Loads TriG content from a stream.
    /// </summary>
    /// <param name="stream">The stream containing TriG data.</param>
    /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
    /// <exception cref="RdfParseException">Thrown when the content cannot be parsed.</exception>
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
    /// Loads TriG content from a TextReader.
    /// </summary>
    /// <param name="reader">The TextReader containing TriG data.</param>
    /// <exception cref="ArgumentNullException">Thrown when reader is null.</exception>
    /// <exception cref="RdfParseException">Thrown when the content cannot be parsed.</exception>
    public void LoadFromTextReader(TextReader reader)
    {
        if (reader == null)
        {
            throw new ArgumentNullException(nameof(reader));
        }

        var parser = new TriGParser();
        var tempStore = new VDS.RDF.TripleStore();
        
        // Provide a default base URI to resolve relative URIs
        var baseUri = new Uri("http://example.org/base/");
        parser.Load(tempStore, reader, baseUri);

        // Transfer loaded data directly to QuadStore
        TransferToQuadStore(tempStore);
    }

    /// <summary>
    /// Loads TriG content from a string.
    /// </summary>
    /// <param name="trigContent">The TriG content as a string.</param>
    /// <exception cref="ArgumentNullException">Thrown when trigContent is null.</exception>
    /// <exception cref="RdfParseException">Thrown when the content cannot be parsed.</exception>
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
    /// <returns>A tuple containing the count of quads in the store.</returns>
    public int GetLoadedQuadCount()
    {
        return _quadStore.Query().Count();
    }

    /// <summary>
    /// Transfers data from a dotNetRDF TripleStore to the QuadStore.
    /// Processes graphs and triples sequentially to stream data directly to the target.
    /// </summary>
    /// <param name="source">The source TripleStore containing the loaded data.</param>
    private void TransferToQuadStore(VDS.RDF.TripleStore source)
    {
        foreach (var graph in source.Graphs)
        {
            // Determine the graph name
            string graphName;
            if (graph.Name == null)
            {
                // Default graph
                graphName = "urn:x-default:default-graph";
            }
            else
            {
                graphName = FormatNode(graph.Name);
            }

            // Transfer all triples from this graph directly to QuadStore
            foreach (var triple in graph.Triples)
            {
                _quadStore.Append(
                    FormatNode(triple.Subject),
                    FormatNode(triple.Predicate),
                    FormatNode(triple.Object),
                    graphName
                );
            }
        }
    }

    /// <summary>
    /// Formats an RDF node as a string suitable for the QuadStore.
    /// </summary>
    private static string FormatNode(INode node)
    {
        return node switch
        {
            IUriNode uriNode => uriNode.Uri.AbsoluteUri,
            IBlankNode blankNode => $"_:{blankNode.InternalID}",
            ILiteralNode literalNode => FormatLiteral(literalNode),
            _ => throw new NotSupportedException($"Node type {node.GetType().Name} is not supported.")
        };
    }

    /// <summary>
    /// Formats a literal node as a string.
    /// </summary>
    private static string FormatLiteral(ILiteralNode literal)
    {
        if (!string.IsNullOrEmpty(literal.Language))
        {
            return $"\"{literal.Value}\"@{literal.Language}";
        }

        if (literal.DataType != null)
        {
            // Use standard XSD namespace URI
            var dataTypeUri = literal.DataType.AbsoluteUri;
            if (dataTypeUri == "http://www.w3.org/2001/XMLSchema#string")
            {
                return $"\"{literal.Value}\"";
            }
            return $"\"{literal.Value}\"^^<{dataTypeUri}>";
        }

        return $"\"{literal.Value}\"";
    }
}
