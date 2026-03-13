using System.IO.MemoryMappedFiles;

namespace TripleStore.Core;

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
            // CRITICAL: Update capacity to match the new mapping size to allow subsequent appends
            _capacity = _length;
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

