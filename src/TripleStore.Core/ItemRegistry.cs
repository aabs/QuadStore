using System.Collections.Concurrent;

namespace TripleStore.Core;

public class ItemRegistry<T>
{
    private int ItemId = -1;

    private readonly ConcurrentDictionary<int, T> forwardLUT = new();
    // Map item hash code to assigned ID so items with identical hash share the same ID
    private readonly ConcurrentDictionary<int, int> reverseLUT = new();

    public int Add(T t)
    {
        ArgumentNullException.ThrowIfNull(t);
        var hash = t.GetHashCode();
        // Atomically assign an ID for the hash code if it doesn't exist
        var id = reverseLUT.GetOrAdd(hash, _ => Interlocked.Increment(ref ItemId));
        // Store the first seen item for this id; subsequent items with same hash keep the original
        forwardLUT.TryAdd(id, t);
        return id;
    }

    public T Lookup(int i)
    {
        return forwardLUT[i];
    }

    public int Get(T t)
    {
        var hash = t?.GetHashCode() ?? 0;
        if (reverseLUT.TryGetValue(hash, out var id))
        {
            return id;
        }
        throw new ApplicationException("not recognised");
    }
}
