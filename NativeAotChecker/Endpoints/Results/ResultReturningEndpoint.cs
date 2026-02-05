using Microsoft.AspNetCore.Http.HttpResults;

namespace NativeAotChecker.Endpoints.Results;

public class ResultReturningEndpoint : EndpointWithoutRequest<Results<NotFound, Ok<string>>>
{
    public override void Configure()
    {
        Get("i-result-returning-endpoint");
        AllowAnonymous();
    }

    public override async Task<Results<NotFound, Ok<string>>> ExecuteAsync(CancellationToken ct)
    {
        await Task.CompletedTask;

        return TypedResults.Ok("hello");
    }
}