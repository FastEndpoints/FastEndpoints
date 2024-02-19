using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using static Microsoft.AspNetCore.Http.TypedResults;

namespace TestCases.TypedResultTest;

sealed class Request
{
    public int Id { get; set; }
}

sealed class Response
{
    public int RequestId { get; set; }
}

[HttpPost("multi-test"), AllowAnonymous]
sealed class MultiResultEndpoint : Endpoint<Request, Results<Ok<Response>, NotFound, ProblemDetails>>
{
    public override async Task<Results<Ok<Response>, NotFound, ProblemDetails>> ExecuteAsync(Request req, CancellationToken ct)
    {
        await Task.Delay(1); //simulate some work

        if (req.Id == 0)
        {
            return NotFound();
        }

        if (req.Id == 1)
        {
            AddError(r => r.Id, "value has to be greater than 1");
            return new ProblemDetails(ValidationFailures);
        }

        return Ok(new Response
        {
            RequestId = req.Id
        });
    }
}