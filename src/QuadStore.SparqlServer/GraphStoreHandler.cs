using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using VDS.RDF;
using VDS.RDF.Storage;

namespace TripleStore.SparqlServer;

internal static class GraphStoreHandler
{
    public static Task<IResult> HandleGet(HttpContext context)
    {
        try
        {
            var storage = context.RequestServices.GetRequiredService<IQueryableStorage>();

            if (storage is not IStorageProvider storageProvider)
            {
                return Task.FromResult(
                    Results.Text("Graph Store Protocol is not supported by the backend.", statusCode: 501));
            }

            var graphParam = context.Request.Query["graph"].FirstOrDefault();

            var graph = new Graph();

            if (!string.IsNullOrWhiteSpace(graphParam))
            {
                storageProvider.LoadGraph(graph, graphParam);
            }
            else
            {
                storageProvider.LoadGraph(graph, (Uri?)null);
            }

            return Task.FromResult(SparqlResultSerializer.SerializeGraph(graph));
        }
        catch (Exception)
        {
            return Task.FromResult(
                Results.Text("An internal error occurred while processing the request.", statusCode: 500));
        }
    }

    public static Task<IResult> HandlePut(HttpContext context)
    {
        return Task.FromResult(
            Results.Text("Graph replacement is not supported by the backend.", statusCode: 501));
    }

    public static Task<IResult> HandlePost(HttpContext context)
    {
        return Task.FromResult(
            Results.Text("Graph merging is not supported by the backend.", statusCode: 501));
    }

    public static Task<IResult> HandleDelete(HttpContext context)
    {
        return Task.FromResult(
            Results.Text("Graph deletion is not supported by the backend.", statusCode: 501));
    }
}
