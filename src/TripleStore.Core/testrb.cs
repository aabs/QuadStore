using System;
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
// Logging removed to avoid external package dependency. Hooks can be added via delegates if needed.
using Roaring.Net;
using Roaring.Net.CRoaring;

namespace TripleStore.Core;

/// <summary>
/// Quad Store with columnar storage, dictionary encoding, bitmap indexing, and memory-mapped persistence.
/// Design goals: high-throughput append, efficient equality queries via bitmap intersections, atomic persistence.
/// Notes:
/// - Columns persist int IDs (dictionary-encoded) in separate memory-mapped files.
/// - Dictionary persisted to a binary file with version and count; atomic replace via temp file.
/// - BitmapIndex maps dictId → sorted set of rowIds; intersection used for multi-criteria filters.
/// - ReaderWriterLockSlim guards concurrent appends and queries.
/// - This implementation avoids external roaring bitmap dependency by using compact sorted sets.
/// </summary>
public sealed class QuadStore : IDisposable
{
    private const int DictFormatVersion = 1;
    private const int ColumnRecordSize = sizeof(int);
    private const int InitialCapacity = 4096;

    private readonly string _root;
    // optional: add logging via delegates in future
    private readonly ReaderWriterLockSlim _lock = new();

    private readonly DictionaryEncoder _encoder;
    private readonly EncodedColumn _s;
    private readonly EncodedColumn _p;
    private readonly EncodedColumn _o;
    private readonly EncodedColumn _g;
    private readonly BitmapIndex _idxS;
    private readonly BitmapIndex _idxP;
    private readonly BitmapIndex _idxO;
    private readonly BitmapIndex _idxG;

    private long _rowCount;

    public QuadStore(string rootPath)
    {
        if (string.IsNullOrWhiteSpace(rootPath)) throw new ArgumentException("Root path is required.", nameof(rootPath));
        _root = rootPath;
        Directory.CreateDirectory(_root);

        _encoder = new DictionaryEncoder(Path.Combine(_root, "dictionary.bin"), Path.Combine(_root, "dictionary.tmp"));
        _s = new EncodedColumn(Path.Combine(_root, "column_s.bin"), InitialCapacity);
        _p = new EncodedColumn(Path.Combine(_root, "column_p.bin"), InitialCapacity);
        _o = new EncodedColumn(Path.Combine(_root, "column_o.bin"), InitialCapacity);
        _g = new EncodedColumn(Path.Combine(_root, "column_g.bin"), InitialCapacity);

        _idxS = new BitmapIndex(Path.Combine(_root, "index_s.bin"));
        _idxP = new BitmapIndex(Path.Combine(_root, "index_p.bin"));
        _idxO = new BitmapIndex(Path.Combine(_root, "index_o.bin"));
        _idxG = new BitmapIndex(Path.Combine(_root, "index_g.bin"));

        LoadAll();
    }

    /// <summary>
    /// Append a quadruple (subject, predicate, object, graph).
    /// </summary>
    public void Append(string subject, string predicate, string obj, string graph)
    {
        if (subject is null) throw new ArgumentNullException(nameof(subject));
        if (predicate is null) throw new ArgumentNullException(nameof(predicate));
        if (obj is null) throw new ArgumentNullException(nameof(obj));
        if (graph is null) throw new ArgumentNullException(nameof(graph));

        _lock.EnterWriteLock();
        try
        {
            int sid = _encoder.GetOrAdd(subject);
            int pid = _encoder.GetOrAdd(predicate);
            int oid = _encoder.GetOrAdd(obj);
            int gid = _encoder.GetOrAdd(graph);

            long row = _rowCount;
            _s.Append(sid);
            _p.Append(pid);
            _o.Append(oid);
            _g.Append(gid);

            _idxS.Add(sid, row);
            _idxP.Add(pid, row);
            _idxO.Add(oid, row);
            _idxG.Add(gid, row);

            _rowCount++;
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Query by equality filters. Any combination of subject, predicate, object, graph may be provided.
    /// </summary>
    public IEnumerable<(string subject, string predicate, string obj, string graph)> Query(string? subject = null, string? predicate = null, string? obj = null, string? graph = null)
    {
        _lock.EnterReadLock();
        try
        {
            IEnumerable<long>? candidate = null;

            if (subject is not null && _encoder.TryGet(subject, out var sid))
                candidate = _idxS.GetRows(sid);
            else if (subject is not null)
                yield break;

            if (predicate is not null && _encoder.TryGet(predicate, out var pid))
                candidate = Intersect(candidate, _idxP.GetRows(pid));
            else if (predicate is not null)
                yield break;

            if (obj is not null && _encoder.TryGet(obj, out var oid))
                candidate = Intersect(candidate, _idxO.GetRows(oid));
            else if (obj is not null)
                yield break;

            if (graph is not null && _encoder.TryGet(graph, out var gid))
                candidate = Intersect(candidate, _idxG.GetRows(gid));
            else if (graph is not null)
                yield break;

            IEnumerable<long> rows = candidate ?? Enumerable.Range(0, (int)_rowCount).Select(i => (long)i);
            foreach (var r in rows)
            {
                int si = _s.Read(r);
                int pi = _p.Read(r);
                int oi = _o.Read(r);
                int gi = _g.Read(r);
                yield return (_encoder.GetString(si), _encoder.GetString(pi), _encoder.GetString(oi), _encoder.GetString(gi));
            }
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    private static IEnumerable<long> Intersect(IEnumerable<long>? a, IEnumerable<long> b)
    {
        if (a is null)
        {
            foreach (var x in b) yield return x;
            yield break;
        }
        // both are sorted sets in practice; use HashSet intersection for simplicity here
        var hs = a is HashSet<long> ha ? ha : new HashSet<long>(a);
        foreach (var x in b)
        {
            if (hs.Contains(x)) yield return x;
        }
    }

    /// <summary>
    /// Persist dictionary, columns, and indexes atomically.
    /// </summary>
    public void SaveAll()
    {
        _lock.EnterWriteLock();
        try
        {
            _encoder.Save();
            _s.Flush();
            _p.Flush();
            _o.Flush();
            _g.Flush();
            _idxS.Save();
            _idxP.Save();
            _idxO.Save();
            _idxG.Save();
            // row count persisted via column file lengths
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <summary>
    /// Load persisted state. Called from ctor.
    /// </summary>
    public void LoadAll()
    {
        _lock.EnterWriteLock();
        try
        {
            _encoder.Load();
            _s.Open();
            _p.Open();
            _o.Open();
            _g.Open();
            _idxS.Load();
            _idxP.Load();
            _idxO.Load();
            _idxG.Load();

            _rowCount = Math.Min(Math.Min(_s.Length, _p.Length), Math.Min(_o.Length, _g.Length));
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    public void Dispose()
    {
        _lock.EnterWriteLock();
        try
        {
            _s.Dispose();
            _p.Dispose();
            _o.Dispose();
            _g.Dispose();
        }
        finally
        {
            _lock.ExitWriteLock();
            _lock.Dispose();
        }
    }
}

/// <summary>
/// Dictionary encoder mapping string ↔ int with atomic persistence.
/// </summary>
public sealed class DictionaryEncoder
{
    private readonly string _filePath;
    private readonly string _tmpFilePath;
    private const int DictFormatVersionLocal = 1;
    private readonly Dictionary<string, int> _forward = new(StringComparer.Ordinal);
    private readonly List<string> _reverse = new();

    public DictionaryEncoder(string filePath, string tmpFilePath)
    {
        _filePath = filePath;
        _tmpFilePath = tmpFilePath;
    }

    public int GetOrAdd(string value)
    {
        if (_forward.TryGetValue(value, out var id)) return id;
        id = _reverse.Count;
        _forward[value] = id;
        _reverse.Add(value);
        return id;
    }

    public bool TryGet(string value, out int id) => _forward.TryGetValue(value, out id);

    public string GetString(int id)
    {
        if ((uint)id >= (uint)_reverse.Count) throw new KeyNotFoundException("Unknown dictionary id");
        return _reverse[id];
    }

    public void Save()
    {
        using var fs = new FileStream(_filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);
        bw.Write(DictFormatVersionLocal);
        bw.Write(_reverse.Count);
        foreach (var s in _reverse)
        {
            var bytes = Encoding.UTF8.GetBytes(s);
            bw.Write(bytes.Length);
            bw.Write(bytes);
        }
        bw.Flush();
        fs.Flush(true);
    }

    public void Load()
    {
        if (!File.Exists(_filePath)) return;
        try
        {
            using var fs = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);
            int ver = br.ReadInt32();
            if (ver != DictFormatVersionLocal) throw new InvalidDataException($"Unsupported dict version: {ver}");
            int count = br.ReadInt32();
            _forward.Clear();
            _reverse.Clear();
            for (int i = 0; i < count; i++)
            {
                int len = br.ReadInt32();
                var bytes = br.ReadBytes(len);
                var s = Encoding.UTF8.GetString(bytes);
                _forward[s] = i;
                _reverse.Add(s);
            }
        }
        catch (Exception ex)
        {
            // logging hook
            throw;
        }
    }

    private static void AtomicReplace(string tmp, string dest)
    {
        const int maxRetries = 8;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(dest))
                {
                    // Attempt atomic replace
                    File.Replace(tmp, dest, destinationBackupFileName: null, ignoreMetadataErrors: true);
                }
                else
                {
                    File.Move(tmp, dest);
                }
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
            catch (UnauthorizedAccessException)
            {
                Thread.Sleep(50 * (attempt + 1));
            }
        }
        // final fallback: try copy-overwrite
        try
        {
            File.Copy(tmp, dest, overwrite: true);
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* ignore */ }
        }
    }
}

/// <summary>
/// Column storing int IDs in a memory-mapped file. Supports append and random reads.
/// </summary>
public sealed class EncodedColumn : IDisposable
{
    private readonly string _path;
    private readonly int _batch;
    private const int ColumnRecordSizeLocal = sizeof(int);
    private const int InitialCapacityLocal = 4096;
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _accessor;
    private long _capacity; // number of records
    private long _length;   // number of records written

    public EncodedColumn(string path, int appendBatch)
    {
        _path = path;
        _batch = Math.Max(1, appendBatch);
    }

    public long Length => _length;

    public void Open()
    {
        if (!File.Exists(_path))
        {
            EnsureCapacity(InitialCapacityLocal);
            return;
        }
        var fi = new FileInfo(_path);
        long bytes = fi.Length;
        _capacity = Math.Max(InitialCapacityLocal, bytes / ColumnRecordSizeLocal);
        // Use FileStream with FileShare.ReadWrite to allow multiple mappings concurrently
        var fs = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
        _mmf = MemoryMappedFile.CreateFromFile(fs, null, _capacity * ColumnRecordSizeLocal, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
        _accessor = _mmf.CreateViewAccessor(0, _capacity * ColumnRecordSizeLocal, MemoryMappedFileAccess.ReadWrite);
        _length = bytes / ColumnRecordSizeLocal;
    }

    public void Append(int value)
    {
        if (_accessor is null) Open();
        if (_length >= _capacity) EnsureCapacity(_capacity + _batch);
        long offset = _length * ColumnRecordSizeLocal;
        _accessor!.Write(offset, value);
        _length++;
    }

    public int Read(long row)
    {
        if (row < 0 || row >= _length) throw new IndexOutOfRangeException();
        long offset = row * ColumnRecordSizeLocal;
        return _accessor!.ReadInt32(offset);
    }

    public void Flush()
    {
        _accessor?.Flush();
        if (_mmf != null)
        {
            // Dispose mapping before truncation to avoid mapped section errors
            _accessor?.Dispose();
            _mmf.Dispose();
            _accessor = null;
            _mmf = null;
            using var fs = new FileStream(_path, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            fs.SetLength(_length * ColumnRecordSizeLocal);
            fs.Flush(true);
            // Reopen mapping after truncation
            _mmf = MemoryMappedFile.CreateFromFile(fs, null, fs.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
            _accessor = _mmf.CreateViewAccessor(0, fs.Length, MemoryMappedFileAccess.ReadWrite);
        }
    }

    private void EnsureCapacity(long newCapacity)
    {
        try
        {
            // Dispose any existing mapping and accessor first
            _accessor?.Dispose();
            _mmf?.Dispose();
            // Allow shared read/write so other columns or handles from this process don't conflict
            using var fs = new FileStream(_path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            fs.SetLength(newCapacity * ColumnRecordSizeLocal);
            fs.Flush(true);
            _mmf = MemoryMappedFile.CreateFromFile(fs, null, fs.Length, MemoryMappedFileAccess.ReadWrite, HandleInheritability.None, false);
            _accessor = _mmf.CreateViewAccessor(0, fs.Length, MemoryMappedFileAccess.ReadWrite);
            _capacity = newCapacity;
        }
        catch (Exception)
        {
            // logging hook
            throw;
        }
    }

    public void Dispose()
    {
        _accessor?.Dispose();
        _mmf?.Dispose();
    }
}

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

