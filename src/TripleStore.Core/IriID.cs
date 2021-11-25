namespace TripleStore.Core;

/// <summary>
///     A wrapper to facilitate avoiding primitive obsessions
/// </summary>
/// <typeparam name="T">The primitive type to be wrapped</typeparam>
public class Wrapper<T>
{
    protected readonly T _value;
    private Wrapper() { }

    public Wrapper(T t)
    {
        _value = t;
    }

    public T Value => _value;
}

public class IriID : Wrapper<ulong>
{
    public IriID(ulong u) : base(u) { }

    public IriID(uint prefix, uint fragment) : this((prefix << MarshallingHelpers.SizeOf<uint>()) | fragment)
    {
    }

    public uint Prefix => (uint)(_value >> MarshallingHelpers.SizeOf<uint>());
    public uint Suffix => (uint)(_value & 0xFFFFFFFF);

    // generate hashcode
    public override int GetHashCode()
    {
        return _value.GetHashCode();
    }
    public static implicit operator ulong(IriID value)
    {
        return value.Value;
    }

    public static implicit operator IriID(ulong value)
    {
        return new IriID(value);
    }
}
