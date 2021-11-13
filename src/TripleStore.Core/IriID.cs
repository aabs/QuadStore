namespace TripleStore.Core;

public class IriID
{
    private readonly uint _id;
    public uint Prefix { get => _id >> 16; }
    public uint Suffix { get => _id & 0xFFFF; }

    public IriID(ushort prefix, ushort fragment)
    {
        var x = prefix << 16;
        x |= fragment;
        _id = (uint)((prefix << 16) | fragment);
    }

    public IriID(uint id)
    {
        _id = id;
    }

    // generate hashcode
    public override int GetHashCode() => _id.GetHashCode();
}
