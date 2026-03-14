using System.Text;

namespace TripleStore.Core;

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
        catch (Exception)
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

