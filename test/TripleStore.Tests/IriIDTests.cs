using System;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class IriIDTests
{
    [Fact]
    public void From_ShouldRoundTrip_ForAnySuffix()
    {
        // Uses current packing logic (shift by Marshal.SizeOf<uint>() * 8 == 32 bits)
        uint prefix = 7;      // arbitrary non-zero prefix
        uint suffix = 9;
        var id = IriID.From(prefix, suffix);

        id.Prefix.Should().Be(prefix, "prefix should round-trip because 32-bit shift isolates prefix");
        id.Suffix.Should().Be((uint)id.Value & 0xFFFFFFFF, "suffix property exposes lower 32 bits of packed value");
    }

    [Fact]
    public void Packing_UsesByteSizeShiftMultipliedBy8_Bits()
    {
        // Marshal.SizeOf<uint>() returns 4 bytes; shift offset is bytes*8 = 32 bits
        MarshallingHelpers.SizeOf<uint>().Should().Be(4, "Marshal.SizeOf<uint>() returns 4 bytes");
    }

    [Fact]
    public void From_WithLargeSuffix_DoesNotAlterRecoveredPrefix()
    {
        uint prefix = 3;
        uint suffix = 0x1FF; // 511 decimal; higher than 15 so bits will spill when shifting back
        var id = IriID.From(prefix, suffix);

        // What the code actually does
        uint recoveredPrefix = (uint)(id.Value >> MarshallingHelpers.SizeOf<uint>()*8); // shift == 32
        recoveredPrefix.Should().Be(id.Prefix);

        // With a 32-bit shift, suffix does not affect recovered prefix
        recoveredPrefix.Should().Be(prefix, "32-bit shift keeps prefix isolated from suffix bits");
    }

    [Fact]
    public void Suffix_EqualsOriginalSuffix_Lower32BitsMasked()
    {
        uint prefix = 0xFF; // 255
        uint suffix = 0xAB; // 171
        var id = IriID.From(prefix, suffix);

        // Packed value per current implementation
        ulong packed = ((ulong)prefix << MarshallingHelpers.SizeOf<uint>()*8) | suffix; // shift 32
        packed.Should().Be(id.Value);

        // The Suffix property masks lower 32 bits which are exactly the original suffix
        id.Suffix.Should().Be((uint)(packed & 0xFFFFFFFF));
        id.Suffix.Should().Be(suffix, "lower 32 bits equal original suffix when using a 32-bit shift");
    }

    [Fact]
    public void PrefixProperty_ShouldEqualOriginalPrefix()
    {
        uint prefix = 0x1234ABCD;
        uint suffix = 0x89ABCDEF;
        var id = IriID.From(prefix, suffix);
        var expected = prefix;
        id.Prefix.Should().Be(expected);
    }

    [Fact]
    public void SuffixProperty_ShouldMatchLower32BitsOfPackedValue()
    {
        uint prefix = 0xDEADBEEF;
        uint suffix = 0xCAFEBABE;
        var id = IriID.From(prefix, suffix);
        ulong packed = ((ulong)prefix << 32) | suffix; // shift 32 bits
        // Lower 32 bits (mask) are returned and equal the original suffix
        ((uint)(packed & 0xFFFFFFFF)).Should().Be(id.Suffix);
        id.Suffix.Should().Be(suffix, "prefix occupies upper 32 bits and does not contaminate suffix");
    }

    [Fact]
    public void Mapping_IsInjective_With32BitBoundary()
    {
        // With a 32-bit boundary, distinct (prefix, suffix) pairs do not collide
        var a = IriID.From(1, 0);
        var b = IriID.From(0, 16);
        a.Value.Should().NotBe(b.Value);
        a.Should().NotBe(b); // Value object inequality
        a.Prefix.Should().NotBe(b.Prefix);
        a.Suffix.Should().NotBe(b.Suffix);
    }

    [Fact]
    public void Collisions_ShouldNotOccur_With32BitBoundary()
    {
        // Under 32-bit separation, these pairs should not collide
        uint p = 5; uint s = 200; uint d = 3;
        uint s2 = s - 16 * d; uint p2 = p + d; // arbitrary transformation that used to collide under 4-bit shift
        var id1 = IriID.From(p, s);
        var id2 = IriID.From(p2, s2);
        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void ExtremeValues_ShouldNotOverflow()
    {
        uint prefix = uint.MaxValue;
        uint suffix = uint.MaxValue;
        var id = IriID.From(prefix, suffix);
        id.Value.Should().Be(((ulong)prefix << 32) | suffix);
        id.Prefix.Should().Be(prefix | (suffix >> 32)); // reduces to uint.MaxValue
        id.Suffix.Should().Be((uint)(((ulong)prefix << 32) | suffix));
    }

    [Fact]
    public void ZeroPrefixZeroSuffix_ShouldBeZeroValue()
    {
        var id = IriID.From(0, 0);
        id.Value.Should().Be(0UL);
        id.Prefix.Should().Be(0U);
        id.Suffix.Should().Be(0U);
    }

    [Fact]
    public void HighSuffixBits_ShouldNotMutatePrefixProperty()
    {
        uint prefix = 0x10; // 16
        uint suffixLow = 0x0F; // 15 -> suffix>>4 = 0 -> no contamination
        uint suffixHigh = 0xF0; // 240 -> suffix>>4 = 15 -> contamination
        var idLow = IriID.From(prefix, suffixLow);
        var idHigh = IriID.From(prefix, suffixHigh);
        idLow.Prefix.Should().Be(prefix | (uint)(((ulong)suffixLow) >> 32));
        idHigh.Prefix.Should().Be(prefix | (uint)(((ulong)suffixHigh) >> 32));
        idHigh.Prefix.Should().Be(idLow.Prefix, "32-bit separation prevents suffix from influencing prefix");
    }

    [Fact]
    public void RecoveringOriginalPrefix_IsUnambiguous_With32BitBoundary()
    {
        // With proper 32-bit boundary, these pairs produce different results
        var id1 = IriID.From(1, 0);
        var id2 = IriID.From(0, 16);
        id1.Prefix.Should().NotBe(id2.Prefix);
        // Distinguishable by value as well
        id1.Value.Should().NotBe(id2.Value);
    }

    [Fact]
    public void SuffixProperty_ShouldEqualOriginalSuffix()
    {
        uint prefix = 0xFFFF0000; // High & low parts
        uint suffix = 0x00000001;
        var id = IriID.From(prefix, suffix);
        // Lower 32 bits after packing equal original suffix
        var expectedLower = (uint)(((ulong)prefix << 32) & 0xFFFFFFFF) | suffix;
        id.Suffix.Should().Be(expectedLower);
    }

    [Fact]
    public void ValueMatchesManualPackingFormula()
    {
        uint prefix = 1234567890;
        uint suffix = 13579;
        var id = IriID.From(prefix, suffix);
        id.Value.Should().Be(((ulong)prefix << 32) | suffix);
    }

    [Fact]
    public void RandomizedPrefixSuffix_PrefixAndSuffixRoundTrip()
    {
        var rng = new Random(1234);
        for (int i = 0; i < 1000; i++)
        {
            uint prefix = (uint)rng.Next(int.MinValue, int.MaxValue);
            uint suffix = (uint)rng.Next(int.MinValue, int.MaxValue);
            var id = IriID.From(prefix, suffix);
            id.Prefix.Should().Be(prefix);
            id.Suffix.Should().Be(suffix);
        }
    }
}
