namespace TripleStore.Core;

/// <summary>
/// Represents parsing errors encountered while processing TriG content.
/// </summary>
public class TrigParseException : Exception
{
    public TrigParseException()
    {
    }

    public TrigParseException(string message) : base(message)
    {
    }

    public TrigParseException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
