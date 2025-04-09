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

        var res = await handler.ExecuteAsync(command, CancellationToken.None);

        res.ShouldBe("a b");
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
            x.Failures!.Count().ShouldBe(2);
            x.Failures!.First().PropertyName.ShouldBe("FirstName");
            x.Failures!.Last().PropertyName.ShouldBe("GeneralErrors");
        }

        handler.ValidationFailures.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CommandHandlerExecsWithoutErrors()
    {
        Factory.RegisterTestServices(_ => { });

        var command = new GetFullName { FirstName = "bobbaa", LastName = "fett" };
        var handler = new MakeFullName(A.Fake<ILogger<MakeFullName>>());

        await handler.ExecuteAsync(command);

        handler.ValidationFailures.Count.ShouldBe(0);
    }

    [Fact]
    public async Task CommandMiddlewareExecutesInCorrectOrder()
    {
        Factory.RegisterTestServices(
            s => s.AddCommandMiddleware(
                      c =>
                      {
                          c.Register<TestCmd, TestResult, FirstMiddleware>();
                          c.Register(typeof(SecondMiddleware<,>), typeof(ThirdMiddleware<,>));
                      })
                  .RegisterTestCommandHandler<TestCmd, TestCmdHandler, TestResult>());

        var handler = new TestCmdHandler();
        handler.RegisterForTesting();

        var result = await new TestCmd().ExecuteAsync(TestContext.Current.CancellationToken);

        result.Output.ShouldBe("[ first-in >> second-in >> third-in >> [handler] << third-out << second-out << first-out ]");
    }
}

sealed class FirstMiddleware : ICommandMiddleware<TestCmd, TestResult>
{
    public async Task<TestResult> ExecuteAsync(TestCmd command, CommandDelegate<TestResult> next, CancellationToken ct)
    {
        command.Input = "[ first-in >> ";
        var result = await next();
        result.Output += "<< first-out ]";

        return result;
    }
}

sealed class SecondMiddleware<TCommand, TResult> : ICommandMiddleware<TCommand, TResult>
    where TCommand : TestCmd, ICommand<TResult>
    where TResult : TestResult
{
    public async Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct)
    {
        command.Input += "second-in >> ";
        var result = await next();
        result.Output += "second-out ";

        return result;
    }
}

sealed class ThirdMiddleware<TCommand, TResult> : ICommandMiddleware<TCommand, TResult>
    where TCommand : TestCmd, ICommand<TResult>
    where TResult : TestResult
{
    public async Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct)
    {
        command.Input += "third-in >> ";
        var result = await next();
        result.Output += "third-out << ";

        return result;
    }
}

class TestCmd : ICommand<TestResult>
{
    public string Input { get; set; } = null!;
}

class TestResult
{
    public string Output { get; set; } = null!;
}

sealed class TestCmdHandler : ICommandHandler<TestCmd, TestResult>
{
    public Task<TestResult> ExecuteAsync(TestCmd cmd, CancellationToken c)
        => Task.FromResult(new TestResult { Output = $"{cmd.Input}[handler] << " });
}