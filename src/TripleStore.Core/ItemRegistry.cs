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
