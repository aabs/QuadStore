using System;
using System.Collections.Generic;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class MultipartUriRegistryBehaviorTests
{
    [Fact]
    public void Add_SameUriTwice_ReturnsSameIds()
    {
        var sut = new MultipartUriRegistry();

        var first = sut.Add("http://www.example.com/dup");
        var second = sut.Add("http://www.example.com/dup");

        second.Prefix.Should().Be(first.Prefix);
        second.Suffix.Should().Be(first.Suffix);
    }

    [Fact]
    public void Add_SamePrefixDifferentSuffix_GeneratesSamePrefixIdAndIncrementingSuffixIds()
    {
        var sut = new MultipartUriRegistry();

        var id1 = sut.Add("http://www.example.com/1");
        var id2 = sut.Add("http://www.example.com/2");

        id1.Prefix.Should().Be(id2.Prefix);
        id1.Suffix.Should().Be(0);
        id2.Suffix.Should().Be(1);
    }

    [Fact]
    public void Lookup_ReturnsOriginalUri_WhenPrefixAndSuffixAreKnown()
    {
        var sut = new MultipartUriRegistry();
        var id = sut.Add("http://www.example.com/alpha");

        var uri = sut.Lookup(id);

        uri.Should().Be(new Uri("http://www.example.com/alpha"));
    }

    [Fact]
    public void Lookup_WithNoRegisteredPrefix_Throws()
    {
        var sut = new MultipartUriRegistry();
        var id = sut.Add("valueWithoutSlash");

        Action act = () => sut.Lookup(id);

        act.Should().Throw<KeyNotFoundException>();
    }

    [Fact]
    public void Get_ReturnsSameIds_AsAdd_ForKnownUris()
    {
        var sut = new MultipartUriRegistry();
        var id1 = sut.Add("http://www.example.com/a");
        var id2 = sut.Add("http://www.example.com/b");

        var lookedUp1 = sut.Get(new Uri("http://www.example.com/a"));
        var lookedUp2 = sut.Get(new Uri("http://www.example.com/b"));

        lookedUp1.Prefix.Should().Be(id1.Prefix);
        lookedUp1.Suffix.Should().Be(id1.Suffix);
        lookedUp2.Prefix.Should().Be(id2.Prefix);
        lookedUp2.Suffix.Should().Be(id2.Suffix);
    }

    [Fact]
    public void Get_UnknownUri_Throws()
    {
        var sut = new MultipartUriRegistry();
        sut.Add("http://www.example.com/a");

        Action act = () => sut.Get(new Uri("http://www.other.com/b"));

        act.Should().Throw<ApplicationException>().WithMessage("not recognised");
    }

    [Fact]
    public void Get_UriWithTrailingSlash_ThrowsForMissingSuffix()
    {
        var sut = new MultipartUriRegistry();
        sut.Add("http://www.example.com/path/");

        Action act = () => sut.Get(new Uri("http://www.example.com/path/"));

        act.Should().Throw<ApplicationException>().WithMessage("not recognised");
    }

    [Fact]
    public void Add_UriWithoutSlash_OnlyRegistersSuffix()
    {
        var sut = new MultipartUriRegistry();
        var id = sut.Add("valueWithoutSlash");

        id.Prefix.Should().Be(0);
        id.Suffix.Should().Be(0);
    }

    [Fact]
    public void DifferentPrefixes_YieldDifferentPrefixIds()
    {
        var sut = new MultipartUriRegistry();

        var a = sut.Add("http://a.example.com/x");
        var b = sut.Add("http://b.example.com/y");

        a.Prefix.Should().NotBe(b.Prefix);
    }

    [Fact]
    public void SameSuffixAcrossDifferentPrefixes_ReusesSuffixId()
    {
        var sut = new MultipartUriRegistry();

        var a = sut.Add("http://a.example.com/z");
        var b = sut.Add("http://b.example.com/z");

        a.Suffix.Should().Be(b.Suffix);
        a.Prefix.Should().NotBe(b.Prefix);
    }
}
