using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;

namespace NativeAotChecker.Endpoints;

public class ResultReturningEndpoint : EndpointWithoutRequest<Results<NotFound, Ok<string>>>
{
    public override void Configure()
    {
        Get("i-result-returning-endpoint");
        AllowAnonymous();
        SerializerContext<ResultReturningEndpointSerCtx>();
    }

    public override async Task<Results<NotFound, Ok<string>>> ExecuteAsync(CancellationToken ct)
    {
        await Task.CompletedTask;

        return TypedResults.Ok("hello");
    }
}

[JsonSerializable(typeof(string))]
public partial class ResultReturningEndpointSerCtx : JsonSerializerContext;