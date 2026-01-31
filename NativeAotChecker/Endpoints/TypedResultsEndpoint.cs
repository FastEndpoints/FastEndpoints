using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http.HttpResults;
using static Microsoft.AspNetCore.Http.TypedResults;

namespace NativeAotChecker.Endpoints;

// Test: IResult / TypedResults returning endpoint in AOT mode
public sealed class TypedResultsRequest
{
    [QueryParam]
    public int Scenario { get; set; } = 1;
}

public sealed class TypedResultsData
{
    public string Message { get; set; } = string.Empty;
    public int Scenario { get; set; }
}

public sealed class TypedResultsEndpoint : Endpoint<TypedResultsRequest, Results<Ok<TypedResultsData>, NotFound, BadRequest<string>>>
{
    public override void Configure()
    {
        Get("typed-results-endpoint");
        AllowAnonymous();
        SerializerContext<TypedResultsSerCtx>();
    }

    public override async Task<Results<Ok<TypedResultsData>, NotFound, BadRequest<string>>> ExecuteAsync(TypedResultsRequest req, CancellationToken ct)
    {
        return req.Scenario switch
        {
            1 => Ok(new TypedResultsData { Message = "Success", Scenario = 1 }),
            2 => NotFound(),
            3 => BadRequest("Bad request scenario"),
            _ => Ok(new TypedResultsData { Message = "Default", Scenario = req.Scenario })
        };
    }
}

// Test: ProblemDetails result
public sealed class ProblemDetailsEndpoint : Endpoint<TypedResultsRequest, Results<Ok<TypedResultsData>, ProblemHttpResult>>
{
    public override void Configure()
    {
        Get("problem-details-endpoint");
        AllowAnonymous();
        SerializerContext<TypedResultsSerCtx>();
    }

    public override async Task<Results<Ok<TypedResultsData>, ProblemHttpResult>> ExecuteAsync(TypedResultsRequest req, CancellationToken ct)
    {
        if (req.Scenario == 1)
        {
            return Ok(new TypedResultsData { Message = "Success", Scenario = 1 });
        }

        return Problem(
            detail: "Something went wrong",
            statusCode: 500,
            title: "Server Error"
        );
    }
}

[JsonSerializable(typeof(TypedResultsRequest))]
[JsonSerializable(typeof(TypedResultsData))]
public partial class TypedResultsSerCtx : JsonSerializerContext;
