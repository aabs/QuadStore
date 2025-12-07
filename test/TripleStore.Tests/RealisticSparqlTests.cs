using System;
using System.Linq;
using FluentAssertions;
using SparqlEngine;
using TripleStore.Core;
using Xunit;

namespace TripleStore.Tests;

public class RealisticSparqlTests
{

    [Fact]
    public void RealisticSparqlQuery_ShouldReturnExpectedResults()
    {
        var store = NewStore();
        LoadDataIntoStore(store, "semopenalex-data-sample.trig");
        var engine = new MinimalSparqlEngine(store);
        var sparqlQuery = """
            PREFIX dcterms: <http://purl.org/dc/terms/>
            PREFIX foaf: <http://xmlns.com/foaf/0.1/>
            PREFIX ns1: <https://semopenalex.org/property/>
            PREFIX ns2: <http://purl.org/spar/datacite/>
            PREFIX ns3: <http://purl.org/spar/cito/>
            PREFIX ns4: <http://purl.org/spar/fabio/>
            PREFIX ns5: <http://purl.org/spar/bido/>
            PREFIX ns6: <https://dbpedia.org/ontology/>
            PREFIX ns7: <http://prismstandard.org/namespaces/basic/2.0/>
            PREFIX ns8: <https://dbpedia.org/property/>
            PREFIX ns9: <http://www.geonames.org/ontology#>
            PREFIX org: <http://www.w3.org/ns/org#>
            PREFIX owl: <http://www.w3.org/2002/07/owl#>
            PREFIX rdfs: <http://www.w3.org/2000/01/rdf-schema#>
            PREFIX skos: <http://www.w3.org/2004/02/skos/core#>
            PREFIX xsd: <http://www.w3.org/2001/XMLSchema#>
            
            SELECT ?s ?o ?g
            WHERE {
                GRAPH ?g {
                    ?s foaf:depiction ?o . 
                }
            }
            """;
            
        var results = engine.ExecuteQuery(sparqlQuery).ToList();
        results.Should().HaveCountGreaterThan(0);
    }

    private void LoadDataIntoStore(QuadStore store, string sampleTrigFilePath)
    {
        var loader = new TriGLoader(store);
        // enumerate all embedded resources to find the correct name
        var resourceNames = GetType().Assembly.GetManifestResourceNames();
        foreach (var resourceName in resourceNames)
        {
            if (resourceName.EndsWith(sampleTrigFilePath))
            {
                sampleTrigFilePath = resourceName;
                break;
            }
        }


        using (var stream = GetType().Assembly.GetManifestResourceStream(sampleTrigFilePath))
        {
            if (stream == null)
            {
                throw new InvalidOperationException($"Resource '{sampleTrigFilePath}' not found.");
            }
            loader.LoadFromStream(stream);

        }

    }

    private static QuadStore NewStore()

    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "sparql_" + Guid.NewGuid());
        System.IO.Directory.CreateDirectory(dir);
        return new QuadStore(dir);
    }
}
