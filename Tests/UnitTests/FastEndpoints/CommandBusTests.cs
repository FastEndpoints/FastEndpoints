using FakeItEasy;
using FastEndpoints;
using Microsoft.Extensions.Logging;
using TestCases.CommandBusTest;
using TestCases.CommandHandlerTest;
using Web.Services;
using Xunit;

namespace CommandBus;

public class CommandBusTests
{
    [Fact]
    public async Task AbilityToFakeTheCommandHandler()
    {
        Factory.RegisterTestServices(_ => { });

        var command = new SomeCommand { FirstName = "a", LastName = "b" };

        var fakeHandler = A.Fake<ICommandHandler<SomeCommand, string>>();
        A.CallTo(() => fakeHandler.ExecuteAsync(A<SomeCommand>.Ignored, A<CancellationToken>.Ignored))
         .Returns(Task.FromResult("Fake Result"));

        fakeHandler.RegisterForTesting();

        var result = await command.ExecuteAsync();

        Assert.Equal("Fake Result", result);
    }

    [Fact]
    public async Task CommandExecutionWorks()
    {
        Factory.RegisterTestServices(_ => { });

        var command = new SomeCommand { FirstName = "a", LastName = "b" };
        var handler = new SomeCommandHandler(A.Fake<ILogger<SomeCommandHandler>>(), A.Fake<IEmailService>());

        var res = await handler.ExecuteAsync(command, default);

        res.Should().Be("a b");
    }

    [Fact]
    public async Task CommandHandlerAddsErrors()
    {
        Factory.RegisterTestServices(_ => { });

        var command = new GetFullName { FirstName = "yoda", LastName = "minch" };
        var handler = new MakeFullName(A.Fake<ILogger<MakeFullName>>());

        try
        {
            await handler.ExecuteAsync(command);
        }
        catch (ValidationFailureException x)
        {
            x.Failures.Should().HaveCount(2);
            x.Failures!.First().PropertyName.Should().Be("FirstName");
            x.Failures!.Last().PropertyName.Should().Be("GeneralErrors");
        }

        handler.ValidationFailures.Should().HaveCount(2);
    }

    [Fact]
    public async Task CommandHandlerExecsWithoutErrors()
    {
        Factory.RegisterTestServices(_ => { });

        var command = new GetFullName { FirstName = "bobbaa", LastName = "fett" };
        var handler = new MakeFullName(A.Fake<ILogger<MakeFullName>>());

        await handler.ExecuteAsync(command);

        handler.ValidationFailures.Should().HaveCount(0);
    }
}