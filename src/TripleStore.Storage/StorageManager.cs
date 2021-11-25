using System.Text;

namespace TripleStore.Storage;

public class StorageManager : LightningProvider, IStorageManager
{

    public DbMetadataBlock GetDbMetadataBlock()
    {
        if (TryGet<byte, DbMetadataBlock>(KeyConstants.DbMetadataBlock, out var result))
        {
            return result;
        }

        return default;
    }
}

public struct DbMetadataBlock
{
    public Version Version { get; set; }
    public Encoding Encoding { get; set; }
    public bool BigEndian { get; set; }
    public byte[] OwnerKey { get; set; }
}

public static class KeyConstants
{
    public const byte DbMetadataBlock = 0x00;
    public const byte IriPrefixRegistry = 0x01;
    public const byte IriSuffixRegistry = 0x02;
    public const byte PropertyStoreRegistry = 0x03;
    public const byte ContentStoreIndex = 0x04;
    public const byte ContentStore = 0x05;

}
