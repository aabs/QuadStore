using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using VDS.RDF.Parsing;
using VDS.RDF.Storage;

namespace TripleStore.SparqlServer;

internal static class SparqlQueryHandler
{
    public static Task<IResult> HandleGet(HttpContext context)
    {
        var query = context.Request.Query["query"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(query))
        {
            return Task.FromResult(Results.Text("Missing required 'query' parameter.", statusCode: 400));
        }

        return ExecuteQueryAsync(context, query);
    }

    public static async Task<IResult> HandlePost(HttpContext context)
    {
        var contentType = context.Request.ContentType ?? string.Empty;

        if (contentType.StartsWith("application/sparql-query", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(context.Request.Body);
            var query = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.Text("Request body is empty.", statusCode: 400);
            }

            return await ExecuteQueryAsync(context, query);
        }

        if (contentType.StartsWith("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            var form = await context.Request.ReadFormAsync();
            var query = form["query"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.Text("Missing required 'query' form field.", statusCode: 400);
            }

            return await ExecuteQueryAsync(context, query);
        }

        if (contentType.StartsWith("application/sparql-update", StringComparison.OrdinalIgnoreCase))
        {
            using var reader = new StreamReader(context.Request.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                return Results.Text("Request body is empty.", statusCode: 400);
            }

            return Results.Text("SPARQL Update is not supported by the backend.", statusCode: 501);
        }

        return Results.Text(
            $"Unsupported Content-Type: {contentType}. Expected application/sparql-query, application/x-www-form-urlencoded, or application/sparql-update.",
            statusCode: 400);
    }

    private static Task<IResult> ExecuteQueryAsync(HttpContext context, string query)
    {
        var storage = context.RequestServices.GetRequiredService<IQueryableStorage>();

        try
        {
            var result = storage.Query(query);
            return Task.FromResult(SparqlResultSerializer.SerializeResult(result));
        }
        catch (Exception ex) when (ex is RdfParseException || ex.InnerException is RdfParseException)
        {
            var parseEx = ex as RdfParseException ?? ex.InnerException as RdfParseException;
            return Task.FromResult(Results.Text(parseEx!.Message, statusCode: 400));
        }
        catch (NotImplementedException ex)
        {
            return Task.FromResult(Results.Text(ex.Message, statusCode: 501));
        }
        catch (RdfStorageException ex) when (ex.Message.Contains("not support"))
        {
            return Task.FromResult(Results.Text(ex.Message, statusCode: 501));
        }
        catch (Exception)
        {
            return Task.FromResult(Results.Text(
                "An internal error occurred while processing the request.",
                statusCode: 500));
        }
    }
}
