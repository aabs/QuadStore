using TripleStore.Core;
using VDS.RDF.Query;
using VDS.RDF.Storage;

// 1) Define a tiny in-memory TriG document with two FOAF name triples.
const string trigContent = """
@prefix foaf: <http://xmlns.com/foaf/0.1/> .

{
  <http://example.org/alice> foaf:name "Alice" .
  <http://example.org/bob> foaf:name "Bob" .
}
""";

// 2) Persist the TriG content to disk so the loader can read it from a file.
var dataFilePath = Path.Combine(AppContext.BaseDirectory, "data.trig");
File.WriteAllText(dataFilePath, trigContent);

// 3) Create/open the QuadStore data directory.
var storePath = Path.Combine(AppContext.BaseDirectory, "store-data");
using var store = new QuadStore(storePath);

// 4) Load TriG quads directly into QuadStore in a single pass.
var loader = new SinglePassTrigLoader(store);
loader.LoadFromFile(dataFilePath);

// 5) Wrap QuadStore with the dotNetRDF storage adapter so Leviathan can query it.
IStorageProvider provider = new QuadStoreStorageProvider(store);
var queryable = (IQueryableStorage)provider;

// 6) Run a SPARQL SELECT query through Leviathan.
var query = @"SELECT ?person ?name
WHERE {
    ?person <http://xmlns.com/foaf/0.1/name> ?name .
}";

var results = (SparqlResultSet)queryable.Query(query);

// 7) Print the SPARQL result rows.
Console.WriteLine("Leviathan query results:");
foreach (var row in results)
{
    Console.WriteLine($"{row["person"]} -> {row["name"]}");
}
