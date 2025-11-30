namespace TripleStore.Core;

public record struct Triple
{
    private static UriRegistry EffectiveIndex { get => RdfCompressionContext.Instance.UriRegistry; }

    public Triple(Uri subject, Uri predicate, Uri @object)
    {
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        Predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
        Object = @object ?? throw new ArgumentNullException(nameof(@object));
    }

    public Triple(int s, int p, int o)
    {
        _subject = s;
        _predicate = p;
        _object = o;
    }

    public (int, int, int) Get()
        => (_subject, _predicate, _object);

    public override int GetHashCode()
    {
        return HashCode.Combine(_subject, _predicate, _object);
    }

    private int _subject;
    public int SubjOrd => _subject;
    public int PredOrd => _predicate;
    public int ObjOrd => _object;

    public Uri Subject
    {
        get => EffectiveIndex.Lookup(_subject);
        set => _subject = EffectiveIndex.Add(value);
    }

    private int _predicate;

    public Uri Predicate
    {
        get => EffectiveIndex.Lookup(_predicate);
        set => _predicate = EffectiveIndex.Add(value);
    }

    private int _object;

    public Uri Object
    {
        get => EffectiveIndex.Lookup(_object);
        set => _object = EffectiveIndex.Add(value);
    }
}
