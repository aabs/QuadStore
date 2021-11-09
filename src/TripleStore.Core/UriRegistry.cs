namespace TripleStore.Core
{
    public class IriID
    {
        private uint _id;
        public uint Prefix { get => _id >> 16; }
        public uint Fragment { get => _id & 0xFFFF; }

        public IriID(ushort prefix, ushort fragment)
        {
            _id = (uint)((prefix << 16) & fragment);
        }

        public IriID(uint id)
        {
            _id = id;
        }

        // generate hashcode
        public override int GetHashCode() => _id.GetHashCode();

    }
    public static class IdUtilities
    {
        public static (string, string) SplitForIndexing(this Uri uri)
        => SplitForIndexing(uri.AbsoluteUri);

        private static (string, string) SplitForIndexing(this string absoluteUri)
        {
            var splitPoint = absoluteUri.LastIndexOf('/');
            if (splitPoint == -1)
            {
                return (null, absoluteUri);
            }
            // split uri into two strings at last '/'
            var prefix = absoluteUri.Substring(0, splitPoint);
            var suffix = absoluteUri.Substring(splitPoint + 1);
            return (prefix, suffix);
        }
    }
    public class UriRegistry
    {
        // LUT for URIs to int
        private int UriId = -1;

        private Dictionary<string, int> prefixMap = new Dictionary<string, int>();
        private Dictionary<int, Uri> lutUris = new Dictionary<int, Uri>();
        private Dictionary<int, int> rlutUris = new Dictionary<int, int>(); // reverse lookup from URI hashcode to ID

        public int Add(Uri u)
        {
            var hashcode = u.GetHashCode();
            if (rlutUris.ContainsKey(hashcode))
            {
                return rlutUris[hashcode];
            }

            var val = Interlocked.Increment(ref UriId);
            rlutUris[hashcode] = val;
            lutUris[val] = u;
            return val;
        }

        public Uri Lookup(int i)
        {
            return lutUris[i];
        }

        public int Get(Uri u)
        {
            var hashCode = u.GetHashCode();
            if (rlutUris.ContainsKey(hashCode))
            {
                return rlutUris[hashCode];
            }
            throw new ApplicationException("URI not recognised");
        }
    }
}