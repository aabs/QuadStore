namespace TripleStore.Core;

public class MultipartUriRegistry
{
    private readonly ItemRegistry<string> _prefixRegistry = new();
    private readonly ItemRegistry<string> _suffixRegistry = new();
    public IriID Add(string uri)
    {
        ushort prefId = ushort.MinValue, suffId = ushort.MinValue;
        (var prefix, var suffix) = uri.SplitForIndexing();
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            prefId = (ushort)_prefixRegistry.Add(prefix);
        }

        if (!string.IsNullOrWhiteSpace(suffix))
        {
            suffId = (ushort)_suffixRegistry.Add(suffix);
        }
        return new IriID(prefId, suffId);
    }
    public Uri Lookup(IriID i)
    {
        var prefix = _prefixRegistry.Lookup((int)i.Prefix);
        var suffix = _suffixRegistry.Lookup((int)i.Suffix);
        if (!string.IsNullOrWhiteSpace(prefix))
        {
            return new Uri($"{prefix}/{suffix}");
        }
        return new Uri(suffix);
    }

    public IriID Get(Uri t)
    {
        (var p, var s) = t.SplitForIndexing();
        var pid = _prefixRegistry.Get(p);
        var sid = _suffixRegistry.Get(s);
        return new IriID((ushort)pid, (ushort)sid);
    }
}
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
public static class IdUtilities
{
    public static (string, string) SplitForIndexing(this Uri uri)
    => SplitForIndexing(uri.AbsoluteUri);

    public static (string, string) SplitForIndexing(this string absoluteUri)
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