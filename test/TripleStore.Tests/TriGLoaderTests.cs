using System;
using System.IO;
using System.Linq;
using FluentAssertions;
using TripleStore.Core;
using VDS.RDF;
using VDS.RDF.Parsing;
using Xunit;

namespace TripleStore.Tests;

public class TriGLoaderTests
{
    private static string NewTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "trig_test_" + Guid.NewGuid());
        Directory.CreateDirectory(dir);
        return dir;
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullQuadStore_ThrowsArgumentNullException()
    {
        // Arrange & Act
        Action act = () => new TriGLoader(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("quadStore");
    }

    [Fact]
    public void Constructor_WithValidQuadStore_InitializesSuccessfully()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);

        // Act
        var loader = new TriGLoader(quadStore);

        // Assert
        loader.Should().NotBeNull();
    }

    #endregion

    #region LoadFromString Tests

    [Fact]
    public void LoadFromString_WithNull_ThrowsArgumentNullException()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromString(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("trigContent");
    }

    [Fact]
    public void LoadFromString_WithSimpleTriG_LoadsSuccessfully()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:subject1 ex:predicate1 ex:object1 .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].subject.Should().Be("http://example.org/subject1");
        quads[0].predicate.Should().Be("http://example.org/predicate1");
        quads[0].obj.Should().Be("http://example.org/object1");
        quads[0].graph.Should().Be("http://example.org/graph1");
    }

    [Fact]
    public void LoadFromString_WithMultipleGraphs_LoadsAllGraphs()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:alice ex:knows ex:bob .
            }
            
            ex:graph2 {
                ex:bob ex:knows ex:charlie .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(2);
        
        quads.Should().Contain(q => 
            q.graph == "http://example.org/graph1" && 
            q.subject == "http://example.org/alice");
        
        quads.Should().Contain(q => 
            q.graph == "http://example.org/graph2" && 
            q.subject == "http://example.org/bob");
    }

    [Fact]
    public void LoadFromString_WithDefaultGraph_LoadsWithDefaultGraphURI()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            {
                ex:subject1 ex:predicate1 ex:object1 .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].graph.Should().Be("urn:x-default:default-graph");
    }

    [Fact]
    public void LoadFromString_WithLiterals_PreservesLiteralValues()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:alice ex:name ""Alice Smith"" .
                ex:bob ex:age 30 .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(2);
        
        var nameQuad = quads.FirstOrDefault(q => q.predicate == "http://example.org/name");
        nameQuad.obj.Should().Contain("Alice Smith");
    }

    [Fact]
    public void LoadFromString_WithBlankNodes_AssignsBlankNodeIdentifiers()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                _:b1 ex:knows _:b2 .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].subject.Should().StartWith("_:");
        quads[0].obj.Should().StartWith("_:");
    }

    [Fact]
    public void LoadFromString_WithInvalidTriG_ThrowsRdfParseException()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var invalidTrigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:subject1 ex:predicate1 
            }
        ";

        // Act
        Action act = () => loader.LoadFromString(invalidTrigContent);

        // Assert
        act.Should().Throw<RdfParseException>();
    }

    [Fact]
    public void LoadFromString_WithMultipleTriplesInOneGraph_LoadsAllTriples()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            @prefix foaf: <http://xmlns.com/foaf/0.1/> .
            
            ex:peopleGraph {
                ex:alice foaf:name ""Alice"" ;
                         foaf:age 30 ;
                         foaf:knows ex:bob .
                
                ex:bob foaf:name ""Bob"" .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(4);
        quads.All(q => q.graph == "http://example.org/peopleGraph").Should().BeTrue();
    }

    [Fact]
    public void LoadFromString_WithTypedLiterals_PreservesDataTypes()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
            
            ex:graph1 {
                ex:item ex:count ""42""^^xsd:integer .
                ex:item ex:price ""19.99""^^xsd:decimal .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(2);
        
        var countQuad = quads.FirstOrDefault(q => q.predicate == "http://example.org/count");
        countQuad.Should().NotBeNull();
        countQuad.obj.Should().Contain("42");
    }

    [Fact]
    public void LoadFromString_WithLanguageTaggedLiterals_PreservesLanguageTags()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:greeting ex:text ""Hello""@en .
                ex:greeting ex:text ""Bonjour""@fr .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(2);
        
        quads.Should().Contain(q => q.obj.Contains("@en"));
        quads.Should().Contain(q => q.obj.Contains("@fr"));
    }

    [Fact]
    public void LoadFromString_WithBaseDirective_ResolvesRelativeIRIs()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @base <http://example.org/> .
            
            <graph1> {
                <subject1> <predicate1> <object1> .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].graph.Should().Be("http://example.org/graph1");
        quads[0].subject.Should().Be("http://example.org/subject1");
    }

    [Fact]
    public void LoadFromString_WithEmptyGraph_LoadsNoQuads()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:emptyGraph { }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().BeEmpty();
    }

    #endregion

    #region LoadFromFile Tests

    [Fact]
    public void LoadFromFile_WithNullPath_ThrowsArgumentNullException()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromFile(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("filePath");
    }

    [Fact]
    public void LoadFromFile_WithEmptyPath_ThrowsArgumentNullException()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromFile("");

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void LoadFromFile_WithNonExistentFile_ThrowsFileNotFoundException()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);
        var nonExistentPath = Path.Combine(dir, "nonexistent.trig");

        // Act
        Action act = () => loader.LoadFromFile(nonExistentPath);

        // Assert
        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void LoadFromFile_WithValidFile_LoadsSuccessfully()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);
        
        var trigFilePath = Path.Combine(dir, "test.trig");
        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:subject1 ex:predicate1 ex:object1 .
            }
        ";
        File.WriteAllText(trigFilePath, trigContent);

        // Act
        loader.LoadFromFile(trigFilePath);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].subject.Should().Be("http://example.org/subject1");
    }

    #endregion

    #region LoadFromStream Tests

    [Fact]
    public void LoadFromStream_WithNullStream_ThrowsArgumentNullException()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromStream(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("stream");
    }

    [Fact]
    public void LoadFromStream_WithValidStream_LoadsSuccessfully()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);
        
        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:subject1 ex:predicate1 ex:object1 .
            }
        ";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(trigContent));

        // Act
        loader.LoadFromStream(stream);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(1);
    }

    #endregion

    #region LoadFromTextReader Tests

    [Fact]
    public void LoadFromTextReader_WithNullReader_ThrowsArgumentNullException()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        Action act = () => loader.LoadFromTextReader(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("reader");
    }

    [Fact]
    public void LoadFromTextReader_WithValidReader_LoadsSuccessfully()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);
        
        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:subject1 ex:predicate1 ex:object1 .
            }
        ";
        var reader = new StringReader(trigContent);

        // Act
        loader.LoadFromTextReader(reader);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(1);
    }

    #endregion

    #region GetLoadedQuadCount Tests

    [Fact]
    public void GetLoadedQuadCount_AfterLoading_ReturnsCorrectCount()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);
        
        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:subject1 ex:predicate1 ex:object1 .
                ex:subject2 ex:predicate2 ex:object2 .
            }
            
            ex:graph2 {
                ex:subject3 ex:predicate3 ex:object3 .
            }
        ";
        loader.LoadFromString(trigContent);

        // Act
        var count = loader.GetLoadedQuadCount();

        // Assert
        count.Should().Be(3);
    }

    [Fact]
    public void GetLoadedQuadCount_BeforeLoading_ReturnsZero()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        // Act
        var count = loader.GetLoadedQuadCount();

        // Assert
        count.Should().Be(0);
    }

    #endregion

    #region Edge Cases and Complex Scenarios

    [Fact]
    public void LoadFromString_WithCollections_LoadsAllElements()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:list ex:contains (ex:item1 ex:item2 ex:item3) .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().NotBeEmpty();
    }

    [Fact]
    public void LoadFromString_WithBlankNodePropertyLists_LoadsCorrectly()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            @prefix foaf: <http://xmlns.com/foaf/0.1/> .
            
            ex:graph1 {
                ex:alice foaf:knows [ foaf:name ""Bob"" ; foaf:age 25 ] .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().NotBeEmpty();
        quads.Count.Should().BeGreaterThan(1);
    }

    [Fact]
    public void LoadFromString_WithMultipleLoadOperations_AccumulatesData()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent1 = @"
            @prefix ex: <http://example.org/> .
            ex:graph1 { ex:alice ex:knows ex:bob . }
        ";
        
        var trigContent2 = @"
            @prefix ex: <http://example.org/> .
            ex:graph2 { ex:bob ex:knows ex:charlie . }
        ";

        // Act
        loader.LoadFromString(trigContent1);
        loader.LoadFromString(trigContent2);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(2);
    }

    [Fact]
    public void LoadFromString_WithGRAPHKeyword_LoadsSuccessfully()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            GRAPH ex:graph1 {
                ex:subject1 ex:predicate1 ex:object1 .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].graph.Should().Be("http://example.org/graph1");
    }

    [Fact]
    public void LoadFromString_WithBooleanLiterals_PreservesValues()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:fact1 ex:isTrue true .
                ex:fact2 ex:isFalse false .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(2);
    }

    [Fact]
    public void LoadFromString_WithUnicodeCharacters_PreservesEncoding()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:person ex:name ""Ålíce 北京 🎉"" .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var quads = quadStore.Query().ToList();
        quads.Should().HaveCount(1);
        quads[0].obj.Should().Contain("Ålíce");
    }

    #endregion

    #region Graph Querying Tests

    [Fact]
    public void LoadFromString_QuerySpecificNamedGraph_ReturnsOnlyThatGraph()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:alice ex:knows ex:bob .
                ex:alice ex:age ""30"" .
            }
            
            ex:graph2 {
                ex:charlie ex:knows ex:diana .
                ex:charlie ex:age ""25"" .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert - Query graph1 via SPARQL GRAPH
        var engine = new SparqlEngine.MinimalSparqlEngine(quadStore);
        var g1Res = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    SELECT ?s ?p ?o WHERE { GRAPH ex:graph1 { ?s ?p ?o } }").ToList();
        g1Res.Should().HaveCount(2);
        g1Res.Should().Contain(r => r["s"] == "<http://example.org/alice>" && r["p"] == "<http://example.org/knows>");
        g1Res.Should().Contain(r => r["s"] == "<http://example.org/alice>" && r["p"] == "<http://example.org/age>");

        // Assert - Query graph2 via SPARQL GRAPH
        var g2Res = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    SELECT ?s ?p ?o WHERE { GRAPH ex:graph2 { ?s ?p ?o } }").ToList();
        g2Res.Should().HaveCount(2);
        g2Res.Should().Contain(r => r["s"] == "<http://example.org/charlie>" && r["p"] == "<http://example.org/knows>");
    }

    [Fact]
    public void LoadFromString_QueryDefaultGraph_ReturnsOnlyDefaultGraph()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            {
                ex:subject1 ex:predicate1 ex:object1 .
                ex:subject2 ex:predicate2 ex:object2 .
            }
            
            ex:graph1 {
                ex:alice ex:knows ex:bob .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert - Query default graph via SPARQL (no GRAPH clause)
        var engine = new SparqlEngine.MinimalSparqlEngine(quadStore);
        var defRes = engine.ExecuteQuery(@"SELECT ?s ?p ?o WHERE { GRAPH <urn:x-default:default-graph> { ?s ?p ?o } }").ToList();
        defRes.Should().HaveCount(2);
        defRes.Select(r => r["s"]).Should().BeEquivalentTo("<http://example.org/subject1>", "<http://example.org/subject2>");

        // Named graph should not contain default graph triples
        var g1Res = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    SELECT ?s ?p ?o WHERE { GRAPH ex:graph1 { ?s ?p ?o } }").ToList();
        g1Res.Should().HaveCount(1);
        g1Res.Select(r => r["s"]).Should().NotContain("<http://example.org/subject1>");
    }

    [Fact]
    public void LoadFromString_QueryBySubjectInSpecificGraph_ReturnsFilteredResults()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:alice ex:knows ex:bob .
                ex:alice ex:age ""30"" .
                ex:bob ex:age ""35"" .
            }
            
            ex:graph2 {
                ex:alice ex:knows ex:charlie .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        var engine = new SparqlEngine.MinimalSparqlEngine(quadStore);
        // alice in graph1
        var g1All = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    SELECT ?s ?p ?o WHERE { GRAPH ex:graph1 { ?s ?p ?o } }").ToList();
        var aliceG1 = g1All.Where(r => r.TryGetValue("s", out var s) && s == "<http://example.org/alice>").ToList();
        aliceG1.Should().HaveCount(2);
        aliceG1.Select(r => r["p"]).Should().BeEquivalentTo("<http://example.org/knows>", "<http://example.org/age>");

        // alice in graph2
        var g2All = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    SELECT ?s ?p ?o WHERE { GRAPH ex:graph2 { ?s ?p ?o } }").ToList();
        var aliceG2 = g2All.Where(r => r.TryGetValue("s", out var s) && s == "<http://example.org/alice>").ToList();
        aliceG2.Should().HaveCount(1);
        aliceG2[0]["p"].Should().Be("<http://example.org/knows>");
    }

    [Fact]
    public void LoadFromString_QueryMultipleGraphsSequentially_ReturnsCorrectData()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            {
                ex:default ex:inDefault ""true"" .
            }
            
            ex:graph1 {
                ex:entity1 ex:property ""value1"" .
            }
            
            ex:graph2 {
                ex:entity2 ex:property ""value2"" .
            }
            
            ex:graph3 {
                ex:entity3 ex:property ""value3"" .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        var engine = new SparqlEngine.MinimalSparqlEngine(quadStore);
        // Default graph (no GRAPH clause)
        var defRes = engine.ExecuteQuery(@"SELECT ?s ?p ?o WHERE { GRAPH <urn:x-default:default-graph> { ?s ?p ?o } }").ToList();
        defRes.Should().HaveCount(1);

        // Named graphs via GRAPH clause
        var g1Res = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    SELECT ?s ?p ?o WHERE { GRAPH ex:graph1 { ?s ?p ?o } }").ToList();
        var g2Res = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    SELECT ?s ?p ?o WHERE { GRAPH ex:graph2 { ?s ?p ?o } }").ToList();
        var g3Res = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    SELECT ?s ?p ?o WHERE { GRAPH ex:graph3 { ?s ?p ?o } }").ToList();

        g1Res.Should().HaveCount(1);
        g2Res.Should().HaveCount(1);
        g3Res.Should().HaveCount(1);

        // Verify subjects
        g1Res[0]["s"].Should().Be("<http://example.org/entity1>");
        g2Res[0]["s"].Should().Be("<http://example.org/entity2>");
        g3Res[0]["s"].Should().Be("<http://example.org/entity3>");
    }

    [Fact]
    public void LoadFromString_QueryNonExistentGraph_ReturnsEmpty()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            
            ex:graph1 {
                ex:alice ex:knows ex:bob .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var engine = new SparqlEngine.MinimalSparqlEngine(quadStore);
        var nonExistent = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    SELECT ?s ?p ?o WHERE { GRAPH ex:nonexistent { ?s ?p ?o } }").ToList();
        nonExistent.Should().BeEmpty();
    }

    [Fact]
    public void LoadFromString_QueryGraphWithComplexData_ReturnsAllTriples()
    {
        // Arrange
        var dir = NewTempDir();
        var quadStore = new QuadStore(dir);
        var loader = new TriGLoader(quadStore);

        var trigContent = @"
            @prefix ex: <http://example.org/> .
            @prefix xsd: <http://www.w3.org/2001/XMLSchema#> .
            
            ex:complexGraph {
                ex:person1 ex:name ""Alice"" ;
                           ex:age ""30""^^xsd:integer ;
                           ex:email ""alice@example.org"" ;
                           ex:knows ex:person2, ex:person3 .
                
                ex:person2 ex:name ""Bob""@en .
                
                _:blank1 ex:property ""value"" .
            }
        ";

        // Act
        loader.LoadFromString(trigContent);

        // Assert
        var engine = new SparqlEngine.MinimalSparqlEngine(quadStore);
        var complexRes = engine.ExecuteQuery(@"PREFIX ex: <http://example.org/>
    PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
    SELECT ?s ?p ?o WHERE { GRAPH ex:complexGraph { ?s ?p ?o } }").ToList();
        complexRes.Count.Should().BeGreaterThan(5);
        complexRes.Should().Contain(r => r["p"] == "<http://example.org/name>");
        complexRes.Should().Contain(r => r["p"] == "<http://example.org/age>");
        complexRes.Should().Contain(r => r["p"] == "<http://example.org/knows>");
    }

    #endregion
}
