using Vogen;

namespace TripleStore.Core;

[ValueObject<ulong>]
public partial struct IriID
{ }

public static class IriIDExtensions
{
    extension(IriID id)
    {
        // create an IriID from prefix and suffix using the vogen From method
        public static IriID From(uint prefix, uint suffix) => IriID.From((prefix << MarshallingHelpers.SizeOf<uint>()) | suffix);
        public uint Prefix => (uint)(id.Value >> MarshallingHelpers.SizeOf<uint>());
        public uint Suffix => (uint)(id.Value & 0xFFFFFFFF);
    }
}
