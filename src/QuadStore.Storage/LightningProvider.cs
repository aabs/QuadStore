using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Text;
using LightningDB;

namespace TripleStore.Storage;

public abstract class LightningProvider : IDisposable
{
    protected LightningEnvironment env;
    public void Dispose()
    {
        env.Dispose();
    }

    protected T FromSpan<T>(ReadOnlySpan<byte> span) where T : struct

    {
        if (span.Length < Unsafe.SizeOf<T>())
            throw new ArgumentException($"Span too small to read type {typeof(T)}. Length={span.Length}, Size={Unsafe.SizeOf<T>()}", nameof(span));

        return MemoryMarshal.Read<T>(span);

    }
    protected ReadOnlySpan<byte> ToSpan<T>(T obj)
    {
        if (obj is byte b) return new[] { b }.AsSpan();
        if (obj is byte[] ba) return ba.AsSpan();
        if (obj is string s) return Encoding.UTF8.GetBytes(s).AsSpan();
        if (obj is long l) return BitConverter.GetBytes(l).AsSpan();
        if (obj is int i) return BitConverter.GetBytes(i).AsSpan();

        // Only known primitive encodings are supported here. Provide explicit encoding for other types.
        throw new NotSupportedException($"Type {typeof(T)} is not supported for binary encoding. Provide explicit encoding for this type.");
    }

    public bool TryGet<TKey, TResult>(TKey key, out TResult result) where TResult : struct
    {
        bool outcome = false;
        result = default;
        using var tx = env.BeginTransaction();
        using var db = tx.OpenDatabase(null, new DatabaseConfiguration { Flags = DatabaseOpenFlags.Create });
        try
        {
            var keyspan = ToSpan(key);
            var (resultCode, _, value) = tx.Get(db, keyspan);
            if (resultCode == MDBResultCode.Success)
            {
                result = FromSpan<TResult>(value.AsSpan());
                outcome = true;
                tx.Commit();
            }
        }
        catch
        {
            // ignored
        }

        return outcome;
    }
    // Note: BinaryFormatter-based serialization removed. Only primitive and unmanaged types are supported for direct binary encoding.
}
