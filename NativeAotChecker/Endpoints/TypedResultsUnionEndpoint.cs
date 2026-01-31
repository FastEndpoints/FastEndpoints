using FastEndpoints;
using Microsoft.AspNetCore.Http.HttpResults;

namespace NativeAotChecker.Endpoints;

// Request for typed results test
public class TypedResultsUnionRequest
{
    public int Id { get; set; }
    public string Action { get; set; } = string.Empty;
}

// Response for typed results
public class TypedResultsUnionResponse
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool TypedResultsWorked { get; set; }
}

/// <summary>
/// Tests Results&lt;T1,T2,...&gt; union types in AOT mode.
/// AOT ISSUE: Results union type uses generic type composition.
/// TypedResults static methods create runtime type instances.
/// ExecuteAsync return type inspection uses reflection.
/// </summary>
public class TypedResultsUnionEndpoint2 : Endpoint<TypedResultsUnionRequest, Results<Ok<TypedResultsUnionResponse>, NotFound, BadRequest<string>>>
{
    public override void Configure()
    {
        Post("typed-results-union2-test");
        AllowAnonymous();
    }

    public override async Task<Results<Ok<TypedResultsUnionResponse>, NotFound, BadRequest<string>>> ExecuteAsync(
        TypedResultsUnionRequest req, 
        CancellationToken ct)
    {
        await Task.CompletedTask;

        if (req.Id == 0)
        {
            return TypedResults.NotFound();
        }

        if (req.Id < 0)
        {
            return TypedResults.BadRequest("Id cannot be negative");
        }

        return TypedResults.Ok(new TypedResultsUnionResponse
        {
            Id = req.Id,
            Status = req.Action,
            TypedResultsWorked = true
        });
    }
}
