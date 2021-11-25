using System.Runtime.InteropServices;
using System.Runtime.Serialization.Formatters.Binary;
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
        return MemoryMarshal.Cast<byte, T>(span).ToArray()[0];

    }
    protected ReadOnlySpan<byte> ToSpan<T>(T obj)
        => obj switch
        {
            byte b => new[] { b }.AsSpan(),
            byte[] ba => ba.AsSpan(),
            string s => new ReadOnlySpan<byte>(Encoding.UTF8.GetBytes(s)),
            long l => BitConverter.GetBytes(l).AsSpan(),
            int i => BitConverter.GetBytes(i).AsSpan(),
            _ => ObjectToByteArray(obj).AsSpan()
        };

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
    private byte[] ObjectToByteArray(Object obj)
    {
        if (obj == null)
            return null;

        BinaryFormatter bf = new BinaryFormatter();
        MemoryStream ms = new MemoryStream();
        bf.Serialize(ms, obj);

        return ms.ToArray();
    }
}
