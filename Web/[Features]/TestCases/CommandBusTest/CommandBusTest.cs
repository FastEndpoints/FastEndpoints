using Microsoft.AspNetCore.Authorization;
using Web.Services;

namespace TestCases.CommandBusTest;

public class SomeCommand : ICommand<string>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class SomeCommandHandler : ICommandHandler<SomeCommand, string>
{
    public SomeCommandHandler(ILogger<SomeCommandHandler> logger, IEmailService emailService)
    {
        logger.LogInformation("command handling works!");
        _ = emailService.SendEmail(); //scoped service
    }

    [AllowAnonymous]
    public Task<string> ExecuteAsync(SomeCommand cmd, CancellationToken ct)
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

public class VoidCommand : ICommand
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public class VoidCommandHandler : ICommandHandler<VoidCommand>
{
    public Task ExecuteAsync(VoidCommand cmd, CancellationToken ct)
    {
        cmd.FirstName = "pass";
        cmd.LastName = "pass";
        return Task.CompletedTask;
    }
}