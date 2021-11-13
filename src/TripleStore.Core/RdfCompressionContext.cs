namespace TripleStore.Core;

/// <summary>
/// A singleton class to handle the shared state involved in compressing RDF
/// </summary>
public sealed class RdfCompressionContext
{
    public UriRegistry UriRegistry { get; } = new UriRegistry();

    private RdfCompressionContext()
    {
    }

    public static RdfCompressionContext Instance { get; } = new RdfCompressionContext();
}
