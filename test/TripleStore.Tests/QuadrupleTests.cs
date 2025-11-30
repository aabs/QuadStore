using System;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class QuadrupleSizeOfTests
{
    [Theory]
    [InlineData(1, typeof(byte))]
    [InlineData(2, typeof(short))]
    [InlineData(4, typeof(int))]
    [InlineData(8, typeof(long))]
    [InlineData(4, typeof(float))]
    [InlineData(8, typeof(double))]
    [InlineData(16, typeof(decimal))]
    public void SizeOf_UnmanagedTypes_ReturnsExpectedSize(int expectedSize, Type type)
    {
        int actual = type switch
        {
            var t when t == typeof(byte) => Quadruple<byte>.SizeOf<byte>(),
            var t when t == typeof(short) => Quadruple<short>.SizeOf<short>(),
            var t when t == typeof(int) => Quadruple<int>.SizeOf<int>(),
            var t when t == typeof(long) => Quadruple<long>.SizeOf<long>(),
            var t when t == typeof(float) => Quadruple<float>.SizeOf<float>(),
            var t when t == typeof(double) => Quadruple<double>.SizeOf<double>(),
            var t when t == typeof(decimal) => Quadruple<decimal>.SizeOf<decimal>(),
            _ => throw new InvalidOperationException("Unsupported test type")
        };

        actual.Should().Be(expectedSize);
    }

    [Fact]
    public void Constructor_DoesNotThrow_ForInt32()
    {
        var action = () => new Quadruple<int>(1, 2, 3, 4);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_DoesNotThrow_ForUInt64()
    {
        var action = () => new Quadruple<ulong>(1, 2, 3, 4);
        action.Should().NotThrow();
    }

    [Fact]
    public void Constructor_DoesNotThrow_ForDecimal()
    {
        var action = () => new Quadruple<decimal>(1m, 2m, 3m, 4m);
        action.Should().NotThrow();
    }
}
