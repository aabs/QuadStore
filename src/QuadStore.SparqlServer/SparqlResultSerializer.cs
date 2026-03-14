using Microsoft.AspNetCore.Http;
using VDS.RDF;
using VDS.RDF.Query;
using VDS.RDF.Writing;

using StringWriter = System.IO.StringWriter;

namespace TripleStore.SparqlServer;

internal static class SparqlResultSerializer
{
    public static IResult SerializeResult(object result)
    {
        if (result is SparqlResultSet resultSet)
        {
            var writer = new SparqlJsonWriter();
            using var sw = new StringWriter();
            writer.Save(resultSet, sw);
            return Results.Content(sw.ToString(), "application/sparql-results+json");
        }

        if (result is IGraph graph)
        {
            return SerializeGraph(graph);
        }

        return Results.Text(result?.ToString() ?? string.Empty, statusCode: 200);
    }

    public static IResult SerializeGraph(IGraph graph)
    {
        var writer = new CompressingTurtleWriter();
        using var sw = new StringWriter();
        writer.Save(graph, sw);
        return Results.Content(sw.ToString(), "text/turtle");
    }
}
