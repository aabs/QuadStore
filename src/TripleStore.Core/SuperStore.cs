namespace TripleStore.Core;

public abstract class SuperStore : ITripleStore
{
    public const int ArraySizeIncrement = 1024;
    protected object _lock = new();
    public Dictionary<int, int> _tripleMap = new();

    public abstract int Count { get; }

    public abstract int InsertTriple(Triple t);

    public Triple NewTriple(Uri subject, Uri predicate, Uri @object)
        => new(subject, predicate, @object);
}
