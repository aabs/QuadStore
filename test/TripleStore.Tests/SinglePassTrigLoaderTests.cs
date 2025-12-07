using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using Xunit;
using TripleStore.Core;

namespace TripleStore.Tests;

/// <summary>
/// Tests for SinglePassTrigLoader - validates the new ANTLR-based loader.
/// </summary>
public class SinglePassTrigLoaderTests
{
    private QuadStore CreateQuadStore()
    {
        var dir = Path.Combine(Path.GetTempPath(), "qs_test_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return new QuadStore(dir);
    }

    [Fact]
    public void LoadFromString_SimpleTriples_LoadsSuccessfully()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);
        var trigContent = @"
<http://example.org/subject> <http://example.org/predicate> <http://example.org/object> .
";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = store.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].Should().Be(("http://example.org/subject", "http://example.org/predicate", "http://example.org/object", "urn:x-default:default-graph"));
    }

    [Fact]
    public void LoadFromString_WithPrefix_ExpandsPrefixesCorrectly()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);
        var trigContent = @"
@prefix ex: <http://example.org/> .
ex:subject ex:predicate ex:object .
";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = store.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].Item1.Should().Be("http://example.org/subject");
        quads[0].Item2.Should().Be("http://example.org/predicate");
        quads[0].Item3.Should().Be("http://example.org/object");
    }

    [Fact]
    public void LoadFromString_MultipleGraphs_IncludesGraphUris()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);
        var trigContent = @"
<http://example.org/graph1> {
  <http://example.org/s1> <http://example.org/p1> <http://example.org/o1> .
}

<http://example.org/graph2> {
  <http://example.org/s2> <http://example.org/p2> <http://example.org/o2> .
}
";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = store.Query().ToList();
        quads.Should().HaveCount(2);
        quads[0].Item4.Should().Be("http://example.org/graph1");
        quads[1].Item4.Should().Be("http://example.org/graph2");
    }

    [Fact]
    public void LoadFromString_PlainLiteral_LoadsCorrectly()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);
        var trigContent = @"
<http://example.org/subject> <http://example.org/name> ""John"" .
";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = store.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].Item3.Should().Contain("John");
    }

    [Fact]
    public void LoadFromString_RdfType_ExpandsCorrectly()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);
        var trigContent = @"
<http://example.org/subject> a <http://example.org/Class> .
";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = store.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].Item2.Should().Contain("type");
    }

    [Fact]
    public void LoadFromString_MultipleTriples_LoadsAllOfThem()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);
        var trigContent = @"
<http://example.org/s1> <http://example.org/p1> <http://example.org/o1> .
<http://example.org/s2> <http://example.org/p2> <http://example.org/o2> .
<http://example.org/s3> <http://example.org/p3> <http://example.org/o3> .
";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = store.Query().ToList();
        quads.Should().HaveCount(3);
    }

    [Fact]
    public void GetLoadedQuadCount_ReturnsCorrectCount()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);
        var trigContent = @"
<http://example.org/s1> <http://example.org/p1> <http://example.org/o1> .
<http://example.org/s2> <http://example.org/p2> <http://example.org/o2> .
";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var count = loader.GetLoadedQuadCount();
        count.Should().Be(2);
    }

    [Fact]
    public void LoadFromString_NullInput_ThrowsArgumentNullException()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => loader.LoadFromString(null!));
    }

    [Fact]
    public void LoadFromFile_NullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => loader.LoadFromFile(null!));
    }

    [Fact]
    public void LoadFromFile_NonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => loader.LoadFromFile("/nonexistent/file.trig"));
    }

    [Fact]
    public void Constructor_NullQuadStore_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new SinglePassTrigLoader(null!));
    }

    [Fact]
    public void LoadFromStream_NullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => loader.LoadFromStream(null!));
    }

    [Fact]
    public void LoadFromTextReader_NullReader_ThrowsArgumentNullException()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => loader.LoadFromTextReader(null!));
    }

    [Fact]
    public void LoadFromString_EmptyTriG_LoadsWithoutError()
    {
        // Arrange
        var store = CreateQuadStore();
        var loader = new SinglePassTrigLoader(store);
        var trigContent = "";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = store.Query().ToList();
        quads.Should().BeEmpty();
    }
}
