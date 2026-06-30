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
    public async Task Service_Registered_Test_Command_Handler_Is_Used()
    {
        Factory.RegisterTestServices(s => s.RegisterTestCommandHandler<ServiceRegisteredCmd, ServiceRegisteredTestHandler, string>());
        ServiceResolver.Instance.Resolve<CommandHandlerRegistry>()[typeof(ServiceRegisteredCmd)] = new(typeof(ServiceRegisteredHandler));
        CommandExtensions.TestCommandHandlerMarker ??= typeof(TestCommandHandlerMarker);

        var result = await new ServiceRegisteredCmd().ExecuteAsync();

        result.ShouldBe("test");
    }

    [Fact]
    public async Task Service_Registered_Test_Void_Command_Handler_Is_Used()
    {
        ServiceRegisteredVoidTestHandler.Result = null;
        Factory.RegisterTestServices(s => s.RegisterTestCommandHandler<ServiceRegisteredVoidCmd, ServiceRegisteredVoidTestHandler>());
        ServiceResolver.Instance.Resolve<CommandHandlerRegistry>()[typeof(ServiceRegisteredVoidCmd)] = new(typeof(ServiceRegisteredVoidHandler));
        CommandExtensions.TestCommandHandlerMarker ??= typeof(TestCommandHandlerMarker);

        await new ServiceRegisteredVoidCmd().ExecuteAsync();

        ServiceRegisteredVoidTestHandler.Result.ShouldBe("test");
    }

    [Fact]
    public async Task Stream_Command_Fake_Handler_Works()
    {
        Factory.RegisterTestServices(_ => { });

        var fakeHandler = A.Fake<IStreamCommandHandler<StreamCmd, int>>();
        A.CallTo(() => fakeHandler.ExecuteAsync(A<StreamCmd>.Ignored, A<CancellationToken>.Ignored))
         .Returns(new[] { 10, 20, 30 }.ToAsyncEnumerable());

        fakeHandler.RegisterForTesting();

        var results = new List<int>();
        await foreach (var item in new StreamCmd(3).ExecuteAsync())
            results.Add(item);

        Assert.Equal([10, 20, 30], results);
    }

    [Fact]
    public async Task Stream_Command_Direct_Execution()
    {
        var handler = new StreamCmdHandler();
        var results = new List<int>();

        await foreach (var item in handler.ExecuteAsync(new StreamCmd(3), CancellationToken.None))
            results.Add(item);

        Assert.Equal([0, 1, 2], results);
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

    [Fact]
    public async Task StreamCommandMiddlewareExecutesInCorrectOrder()
    {
        Factory.RegisterTestServices(
            s => s.AddStreamCommandMiddleware(
                      c =>
                      {
                          c.Register<StreamCmd, int, FirstStreamMiddleware>();
                          c.Register(typeof(SecondStreamMiddleware<,>), typeof(ThirdStreamMiddleware<,>));
                      })
                  .RegisterTestStreamCommandHandler<StreamCmd, StreamCmdHandler, int>());

        var handler = new StreamCmdHandler();
        handler.RegisterForTesting();

        var results = new List<int>();
        await foreach (var item in new StreamCmd(3).ExecuteAsync(TestContext.Current.CancellationToken))
            results.Add(item);

        // handler yields [0,1,2]; FirstStreamMiddleware multiplies by 10 → [0,10,20]
        // Second and ThirdStreamMiddleware are identity pass-throughs (verify chain isn't broken)
        results.ShouldBe([0, 10, 20]);
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

sealed class ServiceRegisteredCmd : ICommand<string>;

sealed class ServiceRegisteredHandler : ICommandHandler<ServiceRegisteredCmd, string>
{
    public Task<string> ExecuteAsync(ServiceRegisteredCmd command, CancellationToken ct)
        => Task.FromResult("real");
}

sealed class ServiceRegisteredTestHandler : ICommandHandler<ServiceRegisteredCmd, string>
{
    public Task<string> ExecuteAsync(ServiceRegisteredCmd command, CancellationToken ct)
        => Task.FromResult("test");
}

sealed class ServiceRegisteredVoidCmd : ICommand;

sealed class ServiceRegisteredVoidHandler : ICommandHandler<ServiceRegisteredVoidCmd>
{
    public Task ExecuteAsync(ServiceRegisteredVoidCmd command, CancellationToken ct)
        => Task.CompletedTask;
}

sealed class ServiceRegisteredVoidTestHandler : ICommandHandler<ServiceRegisteredVoidCmd>
{
    public static string? Result;

    public Task ExecuteAsync(ServiceRegisteredVoidCmd command, CancellationToken ct)
    {
        Result = "test";

        return Task.CompletedTask;
    }
}

public record StreamCmd(int Count) : IStreamCommand<int>;

sealed class StreamCmdHandler : IStreamCommandHandler<StreamCmd, int>
{
    public async IAsyncEnumerable<int> ExecuteAsync(StreamCmd cmd, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        for (var i = 0; i < cmd.Count; i++)
        {
            await Task.Yield();
            yield return i;
        }
    }
}

sealed class FirstStreamMiddleware : IStreamCommandMiddleware<StreamCmd, int>
{
    public async IAsyncEnumerable<int> ExecuteAsync(StreamCmd cmd, StreamCommandDelegate<int> next, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in next().WithCancellation(ct))
            yield return item * 10;
    }
}

sealed class SecondStreamMiddleware<TCommand, TResult> : IStreamCommandMiddleware<TCommand, TResult>
    where TCommand : StreamCmd, IStreamCommand<TResult>
{
    public async IAsyncEnumerable<TResult> ExecuteAsync(TCommand cmd, StreamCommandDelegate<TResult> next, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in next().WithCancellation(ct))
            yield return item;
    }
}

sealed class ThirdStreamMiddleware<TCommand, TResult> : IStreamCommandMiddleware<TCommand, TResult>
    where TCommand : StreamCmd, IStreamCommand<TResult>
{
    public async IAsyncEnumerable<TResult> ExecuteAsync(TCommand cmd, StreamCommandDelegate<TResult> next, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var item in next().WithCancellation(ct))
            yield return item;
    }
}
