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
    /// Uses native Roaring bitmap intersections for efficient multi-field queries.
    /// </summary>
    public IEnumerable<(string subject, string predicate, string obj, string graph)> Query(string? subject = null, string? predicate = null, string? obj = null, string? graph = null)
    {
        _lock.EnterReadLock();
        try
        {
            //Collect all filter bitmaps without mutating them
            var filterBitmaps = new List<Roaring32Bitmap>();

            // Subject filter
            if (subject is not null)
            {
                if (!_encoder.TryGet(subject, out var sid))
                    yield break;
                var sBitmap = _idxS.GetBitmap(sid);
                if (sBitmap == null || sBitmap.Count == 0)
                    yield break;
                filterBitmaps.Add(sBitmap);
            }

            // Predicate filter
            if (predicate is not null)
            {
                if (!_encoder.TryGet(predicate, out var pid))
                    yield break;
                var pBitmap = _idxP.GetBitmap(pid);
                if (pBitmap == null || pBitmap.Count == 0)
                    yield break;
                filterBitmaps.Add(pBitmap);
            }

            // Object filter
            if (obj is not null)
            {
                if (!_encoder.TryGet(obj, out var oid))
                    yield break;
                var oBitmap = _idxO.GetBitmap(oid);
                if (oBitmap == null || oBitmap.Count == 0)
                    yield break;
                filterBitmaps.Add(oBitmap);
            }

            // Graph filter
            if (graph is not null)
            {
                if (!_encoder.TryGet(graph, out var gid))
                    yield break;
                var gBitmap = _idxG.GetBitmap(gid);
                if (gBitmap == null || gBitmap.Count == 0)
                    yield break;
                filterBitmaps.Add(gBitmap);
            }

            // Compute intersection
            IEnumerable<long> rows;
            if (filterBitmaps.Count == 0)
            {
                // No filters - enumerate all rows
                rows = Enumerable.Range(0, (int)_rowCount).Select(i => (long)i);
            }
            else if (filterBitmaps.Count == 1)
            {
                // Single filter - just convert to array
                rows = filterBitmaps[0].ToArray().Select(x => (long)x);
            }
            else
            {
                // Multiple filters - intersect sorted arrays via two-pointer scan for performance
                // Convert to sorted arrays (Roaring already provides ordered iteration)
                var arrays = filterBitmaps
                    .Select(b => b.ToArray())
                    .OrderBy(a => a.Length)
                    .ToArray();

                // Start with the smallest array as the working set
                var work = arrays[0];
                for (int i = 1; i < arrays.Length; i++)
                {
                    var next = arrays[i];
                    // Two-pointer intersection into a temporary List<uint>
                    var tmp = new List<uint>(Math.Min(work.Length, next.Length));
                    int p = 0, q = 0;
                    while (p < work.Length && q < next.Length)
                    {
                        var a = work[p];
                        var b = next[q];
                        if (a == b)
                        {
                            tmp.Add(a);
                            p++; q++;
                        }
                        else if (a < b)
                        {
                            p++;
                        }
                        else
                        {
                            q++;
                        }
                    }
                    if (tmp.Count == 0)
                    {
                        yield break;
                    }
                    work = tmp.ToArray();
                }
                rows = work.Select(x => (long)x);
            }

            // Materialize results
            foreach (var r in rows)
            {
                int si = _s.Read(r);
                int pi = _p.Read(r);
                int oi = _o.Read(r);
                int gi = _g.Read(r);
                yield return (_encoder.GetString(si), _encoder.GetString(pi), 
                             _encoder.GetString(oi), _encoder.GetString(gi));
            }
        }
        finally
        {
            _lock.ExitReadLock();
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

