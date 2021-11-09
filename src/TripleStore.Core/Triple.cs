using System.Diagnostics.CodeAnalysis;

namespace TripleStore.Core;
public class Triple : IEquatable<Triple>
{
    private static UriRegistry EffectiveIndex { get => RdfCompressionContext.Instance.UriRegistry; }

    public Triple(Uri subject, Uri predicate, Uri @object)
    {
        Subject = subject;
        Predicate = predicate;
        Object = @object;
    }

    public Triple(int s, int p, int o)
    {
        _subject = s;
        _predicate = p;
        _object = o;
    }

    public (int, int, int) Get()
        => (_subject, _predicate, _object);

    public override bool Equals(object obj)
    {
        return Equals(obj as Triple);
    }

    public bool Equals([AllowNull] Triple other)
    {
        return other != null &&
               _subject == other._subject &&
               _predicate == other._predicate &&
               _object == other._object;
    }

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

    public static bool operator ==(Triple left, Triple right)
    {
        return EqualityComparer<Triple>.Default.Equals(left, right);
    }

    public static bool operator !=(Triple left, Triple right)
    {
        return !(left == right);
    }
}
