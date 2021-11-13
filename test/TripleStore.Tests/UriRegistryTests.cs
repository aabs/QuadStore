using FluentAssertions;
using NUnit.Framework;
using System;
using TripleStore.Core;

namespace TripleStore.Tests;

[TestFixture]
public class UriRegistryTests
{
    [Test]
    public void TestCanCreateRegistry()
    {
        var sut = new UriRegistry();
        sut.Should().NotBeNull();
    }

    [Test]
    public void TestCanRegisterUri()
    {
        var sut = new UriRegistry();
        var id = sut.Add(new Uri("http://www.example.com/1"));
        id.Should().Be(0);
        var id2 = sut.Add(new Uri("http://www.example.com/2"));
        id2.Should().Be(1);
    }

    [Test]
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

[TestFixture]
public class MultipartUriRegistryTests
{
    [Test]
    public void TestCanCreateRegistry()
    {
        var sut = new MultipartUriRegistry();
        sut.Should().NotBeNull();
    }

    [Test]
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

    [Test]
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
