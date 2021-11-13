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
