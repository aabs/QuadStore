using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using TripleStore.Core;
using VDS.RDF.Parsing;
using Xunit;

namespace TripleStore.Tests;

/// <summary>
/// Tests TriGLoader compliance with W3C TriG specification using official test suite.
/// Test files are available at: https://www.w3.org/2013/TriGTests/
/// </summary>
public class TriGLoaderW3CComplianceTests
{
    private const string W3C_TEST_SUITE_BASE = "https://www.w3.org/2013/TriGTests/";
    private static readonly HttpClient _httpClient = new HttpClient();

    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trig_w3c_test_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    private async Task<string> DownloadTestFileAsync(string fileName)
    {
        var url = $"{W3C_TEST_SUITE_BASE}{fileName}";
        try
        {
            var content = await _httpClient.GetStringAsync(url);
            return content;
        }
        catch (HttpRequestException)
        {
            // If download fails, skip the test
            return null!;
        }
    }

    #region Positive Evaluation Tests (Should Parse Successfully)

    [Fact]
    public async Task W3C_Test_IRI_subject()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("IRI_subject.trig");
        if (testContent == null) return; // Skip if download failed

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
        loader.GetLoadedQuadCount().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task W3C_Test_LITERAL1()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("LITERAL1.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_labeled_blank_node_subject()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("labeled_blank_node_subject.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
        var quads = quadStore.Query().ToList();
        quads.Should().NotBeEmpty();
    }

    [Fact]
    public async Task W3C_Test_bareword_a_predicate()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("bareword_a_predicate.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_old_style_prefix()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("old_style_prefix.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_SPARQL_style_prefix()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("SPARQL_style_prefix.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_langtagged_non_LONG()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("langtagged_non_LONG.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
        var quads = quadStore.Query().ToList();
        quads.Should().Contain(q => q.obj.Contains("@"));
    }

    [Fact]
    public async Task W3C_Test_literal_true()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("literal_true.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_literal_false()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("literal_false.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_bareword_integer()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("bareword_integer.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_bareword_decimal()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("bareword_decimal.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_bareword_double()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("bareword_double.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_blankNodePropertyList_as_object()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("blankNodePropertyList_as_object.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
        loader.GetLoadedQuadCount().Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task W3C_Test_collection_object()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("collection_object.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_empty_collection()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("empty_collection.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_nested_collection()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("nested_collection.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_trig_subm_01()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-subm-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_trig_eval_struct_01()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-eval-struct-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_alternating_iri_graphs()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("alternating_iri_graphs.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
        var quads = quadStore.Query().ToList();
        quads.Should().NotBeEmpty();
    }

    #endregion

    #region Negative Tests (Should Fail to Parse)

    [Fact]
    public async Task W3C_Test_trig_syntax_bad_base_01_ShouldFail()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-bad-base-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().Throw<RdfParseException>();
    }

    [Fact]
    public async Task W3C_Test_trig_syntax_bad_prefix_01_ShouldFail()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-bad-prefix-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().Throw<RdfParseException>();
    }

    [Fact]
    public async Task W3C_Test_trig_syntax_bad_string_01_ShouldFail()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-bad-string-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().Throw<RdfParseException>();
    }

    [Fact]
    public async Task W3C_Test_trig_syntax_bad_num_01_ShouldFail()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-bad-num-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().Throw<RdfParseException>();
    }

    [Fact]
    public async Task W3C_Test_trig_syntax_bad_struct_02_ShouldFail()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-bad-struct-02.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().Throw<RdfParseException>();
    }

    [Fact]
    public async Task W3C_Test_trig_syntax_bad_kw_01_ShouldFail()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-bad-kw-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().Throw<RdfParseException>();
    }

    [Fact]
    public async Task W3C_Test_trig_syntax_bad_list_01_ShouldFail()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-bad-list-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().Throw<RdfParseException>();
    }

    #endregion

    #region Syntax Tests

    [Fact]
    public async Task W3C_Test_trig_syntax_file_01_EmptyFile()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-file-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
        loader.GetLoadedQuadCount().Should().Be(0);
    }

    [Fact]
    public async Task W3C_Test_trig_syntax_prefix_01()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-prefix-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task W3C_Test_trig_syntax_base_01()
    {
        // Arrange
        var testContent = await DownloadTestFileAsync("trig-syntax-base-01.trig");
        if (testContent == null) return;

        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(testContent);

        // Assert
        act.Should().NotThrow();
    }

    #endregion
}
