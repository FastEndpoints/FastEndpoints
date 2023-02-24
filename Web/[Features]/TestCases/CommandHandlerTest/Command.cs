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
