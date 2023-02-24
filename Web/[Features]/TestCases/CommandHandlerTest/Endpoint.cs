namespace TestCases.CommandHandlerTest;

public class Endpoint : EndpointWithoutRequest<string>
{
    public override void Configure()
    {
        Get("/tests/command-handler");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken c)
    {
        AddError("this error was added by the endpoint!");

        Response = await new GetFullName
        {
            FirstName = "yoda",
            LastName = "minch"
        }.ExecuteAsync();
    }
}