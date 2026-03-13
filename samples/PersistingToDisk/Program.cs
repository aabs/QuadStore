using TripleStore.Core;

// Store files are written under the sample's output directory so the sample can run anywhere.
var storePath = Path.Combine(AppContext.BaseDirectory, "store-data");

// All sample assertions are written into a single named graph so they can be queried back together.
var graph = "http://example.org/graphs/people";

// These are the quads that will be persisted in the first session and read back in the second.
var assertions = new (string subject, string predicate, string obj, string graph)[]
{
    ("http://example.org/people/ada", "http://xmlns.com/foaf/0.1/name", "\"Ada Lovelace\"", graph),
    ("http://example.org/people/ada", "http://xmlns.com/foaf/0.1/knows", "http://example.org/people/charles", graph),
    ("http://example.org/people/charles", "http://xmlns.com/foaf/0.1/name", "\"Charles Babbage\"", graph),
};

// Start from an empty on-disk store so each run demonstrates persistence from a clean state.
if (Directory.Exists(storePath))
{
    Directory.Delete(storePath, recursive: true);
}

Console.WriteLine($"Store path: {storePath}");
Console.WriteLine();

Console.WriteLine("Session 1: create an empty persistent store and save assertions.");
using (var firstSession = new QuadStore(storePath))
{
    // A newly created store has no persisted assertions yet.
    Console.WriteLine($"Initial assertion count: {firstSession.Query().Count()}");

    // Append each quad into the store's in-memory structures for this session.
    foreach (var assertion in assertions)
    {
        firstSession.Append(assertion.subject, assertion.predicate, assertion.obj, assertion.graph);
    }

    // Flush dictionaries, columns, and indexes to disk so a later session can reopen them.
    firstSession.SaveAll();

    // Query immediately to show the assertions are present before the store is closed.
    Console.WriteLine($"Saved assertion count: {firstSession.Query(graph: graph).Count()}");
}

Console.WriteLine();
Console.WriteLine("Session 1 closed.");
Console.WriteLine();

Console.WriteLine("Session 2: reopen QuadStore and query the assertions back out");

// Constructing a new QuadStore over the same path reloads the previously persisted files.
using var secondSession = new QuadStore(storePath);

// Query the named graph and materialize the results so they can be printed after reopening.
var persistedAssertions = secondSession.Query(graph: graph).ToList();

Console.WriteLine($"Loaded assertion count: {persistedAssertions.Count}");
foreach (var assertion in persistedAssertions)
{
    Console.WriteLine($"{assertion.subject} {assertion.predicate} {assertion.obj} [{assertion.graph}]");
}
