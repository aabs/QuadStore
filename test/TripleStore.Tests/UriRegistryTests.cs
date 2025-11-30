using System;
using FluentAssertions;
using Xunit;
using TripleStore.Core;

namespace TripleStore.Tests;

public class QuadrupleTests
{
    [Fact]
    public void CanCreateQuadruple()
    {
        var sut = new Quadruple<short>(1, 2, 3, 4);
        sut.Should().NotBeNull();
    }
}

public class UriRegistryTests
{
    [Fact]
    public void TestCanCreateRegistry()
    {
        var sut = new UriRegistry();
        sut.Should().NotBeNull();
    }

    [Fact]
    public void TestCanRegisterUri()
    {
        var sut = new UriRegistry();
        var id = sut.Add(new Uri("http://www.example.com/1"));
        id.Should().Be(0);
        var id2 = sut.Add(new Uri("http://www.example.com/2"));
        id2.Should().Be(1);
    }

    [Fact]
    public void TestCanLookupRegisteredUris()
    {
        var sut = new UriRegistry();
        sut.Add(new Uri("http://www.example.com/1"));
        sut.Add(new Uri("http://www.example.com/2"));
        var id = sut.Get(new Uri("http://www.example.com/1"));
        var id2 = sut.Get(new Uri("http://www.example.com/2"));
        id.Should().Be(0);
        id2.Should().Be(1);
    }
}

public class MultipartUriRegistryTests
{
    [Fact]
    public void TestCanCreateRegistry()
    {
        var sut = new MultipartUriRegistry();
        sut.Should().NotBeNull();
    }

    [Fact]
    public void TestCanRegisterUri()
    {
        var sut = new MultipartUriRegistry();
        var id = sut.Add("http://www.example.com/1");
        id.Prefix.Should().Be(0);
        id.Suffix.Should().Be(0);
        var id2 = sut.Add("http://www.example.com/2");
        id2.Prefix.Should().Be(0);
        id2.Suffix.Should().Be(1);
    }

    [Fact]
    public void TestCanLookupRegisteredUris()
    {
        var sut = new MultipartUriRegistry();
        sut.Add("http://www.example.com/1");
        sut.Add("http://www.example.com/2");
        var id = sut.Get(new Uri("http://www.example.com/1"));
        var id2 = sut.Get(new Uri("http://www.example.com/2"));
        id.Prefix.Should().Be(0);
        id2.Prefix.Should().Be(0);
    }
}
