using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace TripleStore.SparqlServer;

public static class SparqlEndpointRouteBuilderExtensions
{
    public static IEndpointRouteBuilder MapSparqlEndpoints(
        this IEndpointRouteBuilder endpoints,
        string routePrefix = "/sparql")
    {
        // SPARQL Query endpoints
        endpoints.MapGet(routePrefix, async (HttpContext context) =>
        {
            var result = await SparqlQueryHandler.HandleGet(context);
            await result.ExecuteAsync(context);
        });

        endpoints.MapPost(routePrefix, async (HttpContext context) =>
        {
            var result = await SparqlQueryHandler.HandlePost(context);
            await result.ExecuteAsync(context);
        });

        // Graph Store Protocol endpoints
        var graphRoute = $"{routePrefix}/graph";

        endpoints.MapGet(graphRoute, async (HttpContext context) =>
        {
            var result = await GraphStoreHandler.HandleGet(context);
            await result.ExecuteAsync(context);
        });

        endpoints.MapPut(graphRoute, async (HttpContext context) =>
        {
            var result = await GraphStoreHandler.HandlePut(context);
            await result.ExecuteAsync(context);
        });

        endpoints.MapPost(graphRoute, async (HttpContext context) =>
        {
            var result = await GraphStoreHandler.HandlePost(context);
            await result.ExecuteAsync(context);
        });

        endpoints.MapDelete(graphRoute, async (HttpContext context) =>
        {
            var result = await GraphStoreHandler.HandleDelete(context);
            await result.ExecuteAsync(context);
        });

        return endpoints;
    }
}
