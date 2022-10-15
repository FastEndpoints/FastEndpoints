using Microsoft.AspNetCore.Authorization;

namespace TestCases.EmptyRequestTest;

[HttpGet("/test-cases/empty-request-test")]
[Authorize(Roles = Role.Admin)]
public class EmptyRequestEndpoint : Endpoint<EmptyRequest, EmptyResponse>
{
    public async override Task HandleAsync(EmptyRequest req, CancellationToken ct) => await SendOkAsync(ct);
}