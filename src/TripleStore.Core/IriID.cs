using Vogen;

namespace TripleStore.Core;

[ValueObject<ulong>]
public partial struct IriID
{ }

public static class IriIDExtensions
{
    const int ShiftOffset = 32; // i.e.  MarshallingHelpers.SizeOf<uint>() * 8;
    extension(IriID id)
    {
        // create an IriID from prefix and suffix using the vogen From method
        // cast prefix to ulong before shifting so that a 64-bit shift occurs; otherwise (uint << 32) yields the original value
        public static IriID From(uint prefix, uint suffix) => IriID.From((((ulong)prefix) << ShiftOffset) | suffix);
        public uint Prefix => (uint)(id.Value >> ShiftOffset);
        public uint Suffix => (uint)(id.Value & 0xFFFFFFFF);
    }
}
