using Vogen;

namespace TripleStore.Core;

[ValueObject<ulong>]
public partial struct IriID
{ }

public static class IriIDExtensions
{
    static int ShiftOffset = MarshallingHelpers.SizeOf<uint>() * 8;
    extension(IriID id)
    {
        public static IriID From(uint prefix, uint suffix) => IriID.From((((ulong)prefix) << ShiftOffset) | suffix);
        public uint Prefix => (uint)(id.Value >> ShiftOffset);
        public uint Suffix => (uint)(id.Value & 0xFFFFFFFF);
    }
}
