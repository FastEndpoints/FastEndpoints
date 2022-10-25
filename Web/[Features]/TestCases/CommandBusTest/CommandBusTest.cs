using Web.Services;

namespace TestCases.CommandBusTest;

public class TestCommand : ICommand<string>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class TestCommandHandler : FastCommandHandler<TestCommand, string>
{
    public TestCommandHandler(ILogger<TestCommandHandler> logger, IEmailService emailService)
    {
        logger.LogError("command handling works!");
        _ = emailService.SendEmail(); //scoped service
    }

    public override Task<string> ExecuteAsync(TestCommand cmd, CancellationToken ct)
    {
        return Task.FromResult(cmd.FirstName + " " + cmd.LastName);
    }
}

public class TestVoidCommand : ICommand
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class TestVoidCommandHandler : FastCommandHandler<TestVoidCommand>
{
    public override Task ExecuteAsync(TestVoidCommand cmd, CancellationToken ct)
    {
        cmd.FirstName = "pass";
        cmd.LastName = "pass";
        return Task.CompletedTask;
    }
}