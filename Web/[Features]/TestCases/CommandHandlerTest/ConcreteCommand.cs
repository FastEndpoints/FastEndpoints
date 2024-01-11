namespace TestCases.CommandHandlerTest;

public class GetFullName : ICommand<string>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class MakeFullName : CommandHandler<GetFullName, string>
{
    public MakeFullName(ILogger<MakeFullName> logger)
    {
        logger.LogInformation("di works!");
    }

    public override Task<string> ExecuteAsync(GetFullName cmd, CancellationToken ct = default)
    {
        if (cmd.FirstName.Length < 5)
            AddError(c => c.FirstName, "your first name is too short!");

        if (cmd.FirstName == "yoda")
            ThrowError("no jedi allowed here!");

        ThrowIfAnyErrors();

        return Task.FromResult(cmd.FirstName + " " + cmd.LastName);
    }
}

public class ConcreteCmdEndpoint : EndpointWithoutRequest<string>
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