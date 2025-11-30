using System;
using FluentAssertions;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class IdUtilitiesTests
{
    [Theory]
    [InlineData("http://example.com/a/b", "http://example.com/a", "b")]
    [InlineData("http://example.com/", "http://example.com", "")]
    [InlineData("noslash", null, "noslash")]
    [InlineData("/", "", "")]
    [InlineData("", null, "")]
    [InlineData("http://example.com", null, "http://example.com")]
    [InlineData("http://x/a/b?q=1#frag", "http://x/a", "b?q=1#frag")]
    [InlineData("//", "/", "")]
    [InlineData("///a", "//", "a")]
    [InlineData("urn:foo:bar", null, "urn:foo:bar")]
    [InlineData("file:///C:/path", "file://", "C:/path")]
    [InlineData("/παράδειγμα/%E2%9C%93", "/παράδειγμα", "%E2%9C%93")]
    [InlineData(" /a/b ", " /a", "b ")]
    [InlineData("http://x\\a\\b", "http://x\\a", "b")]
    public void SplitForIndexing_String_ReturnsExpectedParts(string input, string expectedPrefix, string expectedSuffix)
    {
        var (prefix, suffix) = IdUtilities.SplitForIndexing(input);

        prefix.Should().Be(expectedPrefix);
        suffix.Should().Be(expectedSuffix);
    }

    [Fact]
    public void SplitForIndexing_Uri_ReturnsExpectedParts()
    {
        var uri = new Uri("https://domain.test/path/to/resource");

        var (prefix, suffix) = IdUtilities.SplitForIndexing(uri);

        prefix.Should().Be("https://domain.test/path/to");
        suffix.Should().Be("resource");
    }

    [Theory]
    [InlineData("https://domain.test", null, "https://domain.test")]
    [InlineData("https://domain.test/", "https://domain.test", "")]
    [InlineData("https://domain.test/a//b", "https://domain.test/a/", "b")]
    [InlineData("mailto:user@example.com", null, "mailto:user@example.com")]
    public void SplitForIndexing_Uri_CornerCases(string uriString, string expectedPrefix, string expectedSuffix)
    {
        var uri = new Uri(uriString);
        var (prefix, suffix) = IdUtilities.SplitForIndexing(uri);

        prefix.Should().Be(expectedPrefix);
        suffix.Should().Be(expectedSuffix);
    }
}
