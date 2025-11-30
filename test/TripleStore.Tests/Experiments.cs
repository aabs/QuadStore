using System;
using FluentAssertions;
using Xunit;


namespace TripleStore.Tests;

public class Experiments
{


    [Fact]
    public void MemoryOverArrayOfBytesTest()
    {
        var buf = new byte[1024];
        var sut = new Memory<byte>(buf, 0, 8);
        var span = sut.Span; // mutate underlying buffer through the span view
        span[0] = 0x1;
        span[1] = 0x2;
        buf[0].Should().Be(0x1);
        buf[1].Should().Be(0x2);
    }


}
