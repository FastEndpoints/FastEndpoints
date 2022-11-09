using Web.Services;

namespace TestCases.CommandBusTest;

public class TestCommand : ICommand<string>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class TestCommandHandler : ICommandHandler<TestCommand, string>
{
    public TestCommandHandler(ILogger<TestCommandHandler> logger, IEmailService emailService)
    {
        logger.LogInformation("command handling works!");
        _ = emailService.SendEmail(); //scoped service
    }

    public Task<string> ExecuteAsync(TestCommand cmd, CancellationToken ct)
    {
        return Task.FromResult(cmd.FirstName + " " + cmd.LastName);
    }
}

public class TestVoidCommand : ICommand
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class TestVoidCommandHandler : ICommandHandler<TestVoidCommand>
{
    public Task ExecuteAsync(TestVoidCommand cmd, CancellationToken ct)
    {
        cmd.FirstName = "pass";
        cmd.LastName = "pass";
        return Task.CompletedTask;
    }
}