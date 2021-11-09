namespace TripleStore.Core;

public class ItemRegistry<T>
{
    private int ItemId = -1;

    private readonly Dictionary<int, T> forwardLUT = new();
    private readonly Dictionary<int, int> reverseLUT = new(); // reverse lookup from URI hashcode to ID
    public int Add(T t)
    {
        var hashcode = t.GetHashCode();
        if (reverseLUT.ContainsKey(hashcode))
        {
            return reverseLUT[hashcode];
        }

        var val = Interlocked.Increment(ref ItemId);
        reverseLUT[hashcode] = val;
        forwardLUT[val] = t;
        return val;
    }

    public T Lookup(int i)
    {
        return forwardLUT[i];
    }

    public int Get(T t)
    {
        var hashCode = t.GetHashCode();
        if (reverseLUT.ContainsKey(hashCode))
        {
            return reverseLUT[hashCode];
        }
        throw new ApplicationException("not recognised");
    }
}
public class UriRegistry : ItemRegistry<Uri> {}

public class IriID
{
    private readonly uint _id;
    public uint Prefix { get => _id >> 16; }
    public uint Fragment { get => _id & 0xFFFF; }

    public IriID(ushort prefix, ushort fragment)
    {
        _id = (uint)((prefix << 16) & fragment);
    }

    public IriID(uint id)
    {
        _id = id;
    }

    // generate hashcode
    public override int GetHashCode() => _id.GetHashCode();

}
public static class IdUtilities
{
    public static (string, string) SplitForIndexing(this Uri uri)
    => SplitForIndexing(uri.AbsoluteUri);

    private static (string, string) SplitForIndexing(this string absoluteUri)
    {
        var splitPoint = absoluteUri.LastIndexOf('/');
        if (splitPoint == -1)
        {
            return (null, absoluteUri);
        }
        // split uri into two strings at last '/'
        var prefix = absoluteUri[..splitPoint];
        var suffix = absoluteUri[(splitPoint + 1)..];
        return (prefix, suffix);
    }
}