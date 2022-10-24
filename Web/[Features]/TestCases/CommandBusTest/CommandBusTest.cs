namespace TestCases.CommandBusTest;

public class TestCommand : ICommand<string>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class TestCommandHandler : FastCommandHandler<TestCommand, string>
{
    public override Task<string> ExecuteAsync(TestCommand cmd, CancellationToken ct)
    {
        return Task.FromResult(cmd.FirstName + " " + cmd.LastName);
    }
}