using System.Collections.Concurrent;
using System.Text;
using Roaring.Net.CRoaring;

namespace TripleStore.Core;

/// <summary>
/// Bitmap index mapping dictionary id → set of row ids. Persisted compactly.
/// </summary>
public sealed class BitmapIndex
{
    private readonly string _path;
    private readonly ConcurrentDictionary<int, Roaring32Bitmap> _bitmaps = new();

    public BitmapIndex(string path)
    {
        _path = path;
    }

    public void Add(int dictId, long row)
    {
        if (row < int.MinValue || row > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(row));
        var bm = _bitmaps.GetOrAdd(dictId, _ => new Roaring32Bitmap());
        bm.Add((uint)row);
    }

    public IEnumerable<long> GetRows(int dictId)
    {
        if (_bitmaps.TryGetValue(dictId, out var bm))
        {
            // Enumerate using array snapshot to avoid direct foreach on bitmap
            var arr = bm.ToArray();
            // Roaring returns sorted values; cast to long
            for (int i = 0; i < arr.Length; i++)
            {
                yield return (long)arr[i];
            }
            yield break;
        }
        yield break;
    }

    /// <summary>
    /// Get the bitmap for a dictionary ID. Returns null if the ID doesn't exist.
    /// Avoids array conversion for efficient native bitmap operations.
    /// </summary>
    public Roaring32Bitmap? GetBitmap(int dictId)
    {
        return _bitmaps.TryGetValue(dictId, out var bm) ? bm : null;
    }

    public void Save()
    {
        try
        {
            using var fs = new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None);
            using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);
            bw.Write(1); // version
            bw.Write(_bitmaps.Count);
            foreach (var kvp in _bitmaps.OrderBy(k => k.Key))
            {
                bw.Write(kvp.Key);
                // Write count and then all row ids as Int64 for forward compatibility
                var arr = kvp.Value.ToArray();
                bw.Write(arr.Length);
                for (int i = 0; i < arr.Length; i++)
                {
                    bw.Write((long)arr[i]);
                }
            }
            bw.Flush();
            fs.Flush(true);
        }
        catch (Exception)
        {
            throw;
        }
    }

    public void Load()
    {
        if (!File.Exists(_path)) return;
        try
        {
            using var fs = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);
            int ver = br.ReadInt32();
            if (ver != 1) throw new InvalidDataException("Unsupported bitmap index version");
            int keys = br.ReadInt32();
            _bitmaps.Clear();
            for (int i = 0; i < keys; i++)
            {
                int key = br.ReadInt32();
                int cnt = br.ReadInt32();
                var bm = new Roaring32Bitmap();
                for (int j = 0; j < cnt; j++)
                {
                    long r = br.ReadInt64();
                    bm.Add((uint)r);
                }
                _bitmaps[key] = bm;
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    private static int QuadStoreInitialCapacity() => 4096; // mirror QuadStore.InitialCapacity

    private static void AtomicReplace(string tmp, string dest)
    {
        if (File.Exists(dest)) File.Delete(dest);
        File.Move(tmp, dest);
    }
}

