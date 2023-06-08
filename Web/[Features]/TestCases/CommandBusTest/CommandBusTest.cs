using Microsoft.AspNetCore.Authorization;
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

    [AllowAnonymous]
    public Task<string> ExecuteAsync(TestCommand cmd, CancellationToken ct)
    {
        return Task.FromResult(cmd.FirstName + " " + cmd.LastName);
    }
}

public class EchoCommand : ICommand<EchoCommand>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class EchoCommandHandler : ICommandHandler<EchoCommand, EchoCommand>
{
    public EchoCommandHandler(ILogger<EchoCommandHandler> logger, IEmailService emailService)
    {
        logger.LogInformation("command handling works!");
        _ = emailService.SendEmail(); //scoped service
    }

    public Task<EchoCommand> ExecuteAsync(EchoCommand cmd, CancellationToken ct)
    {
        return Task.FromResult(new EchoCommand
        {
            FirstName = cmd.FirstName,
            LastName = cmd.LastName
        });
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