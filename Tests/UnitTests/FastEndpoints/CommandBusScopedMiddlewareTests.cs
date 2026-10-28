using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CommandBus;

[Collection(CommandBusTestCollection.Name)]
public class CommandBusScopedMiddlewareTests
{
    [Theory]
    [InlineData(false, 0)]
    [InlineData(false, 1)]
    [InlineData(false, 2)]
    [InlineData(false, 3)]
    [InlineData(true, 0)]
    [InlineData(true, 1)]
    [InlineData(true, 2)]
    [InlineData(true, 3)]
    public async Task Middleware_Uses_Current_Scope_And_Fresh_Transient_Dependencies(bool stream, int registrationStyle)
    {
        Factory.RegisterTestServices(_ => { });
        var previousResolver = ServiceResolver.Instance;
        var services = new ServiceCollection();
        services.AddHttpContextAccessor();
        services.AddScoped<ScopedDependency>();
        services.AddTransient<TransientDependency>();
        services.AddMessaging(new List<Type> { stream ? typeof(StreamHandler) : typeof(ResultHandler) });

        if (stream)
        {
            if (registrationStyle is 0 or 3)
                services.AddStreamCommandMiddleware(c => c.Register<StreamCommand, string, ClosedStreamMiddleware>());
            else
                services.AddTransient<IStreamCommandMiddleware<StreamCommand, string>, ClosedStreamMiddleware>();

            if (registrationStyle is 0 or 2)
                services.AddStreamCommandMiddleware(c => c.Register(typeof(StreamMiddleware<,>)));
            else
                services.AddTransient(typeof(IStreamCommandMiddleware<,>), typeof(StreamMiddleware<,>));
        }
        else
        {
            if (registrationStyle is 0 or 3)
                services.AddCommandMiddleware(c => c.Register<ResultCommand, string, ClosedResultMiddleware>());
            else
                services.AddTransient<ICommandMiddleware<ResultCommand, string>, ClosedResultMiddleware>();

            if (registrationStyle is 0 or 2)
                services.AddCommandMiddleware(c => c.Register(typeof(ResultMiddleware<,>)));
            else
                services.AddTransient(typeof(ICommandMiddleware<,>), typeof(ResultMiddleware<,>));
        }

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        var accessor = provider.GetRequiredService<IHttpContextAccessor>();
        var previousContext = accessor.HttpContext;
        var commands = new List<MiddlewareCommand>();

        try
        {
            provider.UseMessaging();
            object? executor = null;

            for (var i = 0; i < 2; i++)
            {
                using var scope = provider.CreateScope();
                accessor.HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider };

                for (var j = 0; j < 2; j++)
                {
                    MiddlewareCommand command;

                    if (stream)
                    {
                        var cmd = new StreamCommand();
                        var results = new List<string>();
                        await foreach (var result in cmd.ExecuteAsync(TestContext.Current.CancellationToken))
                            results.Add(result);
                        results.ShouldBe(["ok"]);
                        command = cmd;
                    }
                    else
                    {
                        var cmd = new ResultCommand();
                        (await cmd.ExecuteAsync(TestContext.Current.CancellationToken)).ShouldBe("ok");
                        command = cmd;
                    }

                    command.Log.ShouldBe("closed-in>open-in>[handler]<open-out<closed-out");
                    command.ScopedDependencies.ShouldBe(Enumerable.Repeat(scope.ServiceProvider.GetRequiredService<ScopedDependency>().Id, 2));
                    commands.Add(command);

                    var currentExecutor = provider.GetRequiredService<CommandHandlerRegistry>()[command.GetType()].HandlerExecutor;
                    currentExecutor.ShouldNotBeNull();
                    executor ??= currentExecutor;
                    currentExecutor.ShouldBeSameAs(executor);
                }
            }

            commands[0].ScopedDependencies.ShouldBe(commands[1].ScopedDependencies);
            commands[2].ScopedDependencies.ShouldBe(commands[3].ScopedDependencies);
            commands[0].ScopedDependencies[0].ShouldNotBe(commands[2].ScopedDependencies[0]);
            commands.SelectMany(c => c.TransientDependencies).Distinct().Count().ShouldBe(8);
        }
        finally
        {
            accessor.HttpContext = previousContext;
            ServiceResolver.Instance = previousResolver;
        }
    }

    public class MiddlewareCommand
    {
        public string Log { get; set; } = "";
        public List<Guid> ScopedDependencies { get; } = [];
        public List<Guid> TransientDependencies { get; } = [];
    }

    public class ResultCommand : MiddlewareCommand, ICommand<string>;
    public class StreamCommand : MiddlewareCommand, IStreamCommand<string>;

    public class ScopedDependency
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public class TransientDependency
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    public class ResultHandler : ICommandHandler<ResultCommand, string>
    {
        public Task<string> ExecuteAsync(ResultCommand command, CancellationToken ct)
        {
            command.Log += "[handler]";

            return Task.FromResult("ok");
        }
    }

    public class StreamHandler : IStreamCommandHandler<StreamCommand, string>
    {
        public IAsyncEnumerable<string> ExecuteAsync(StreamCommand command, CancellationToken ct)
        {
            command.Log += "[handler]";

            return new[] { "ok" }.ToAsyncEnumerable();
        }
    }

    public class ResultMiddleware<TCommand, TResult>(ScopedDependency scoped, TransientDependency transient)
        : ICommandMiddleware<TCommand, TResult> where TCommand : ICommand<TResult>
    {
        protected virtual string Label => "open";

        public async Task<TResult> ExecuteAsync(TCommand command, CommandDelegate<TResult> next, CancellationToken ct)
        {
            var cmd = (MiddlewareCommand)(object)command;
            cmd.ScopedDependencies.Add(scoped.Id);
            cmd.TransientDependencies.Add(transient.Id);
            cmd.Log += $"{Label}-in>";
            var result = await next();
            cmd.Log += $"<{Label}-out";

            return result;
        }
    }

    public class ClosedResultMiddleware(ScopedDependency scoped, TransientDependency transient)
        : ResultMiddleware<ResultCommand, string>(scoped, transient)
    {
        protected override string Label => "closed";
    }

    public class StreamMiddleware<TCommand, TResult>(ScopedDependency scoped, TransientDependency transient)
        : IStreamCommandMiddleware<TCommand, TResult> where TCommand : IStreamCommand<TResult>
    {
        protected virtual string Label => "open";

        public async IAsyncEnumerable<TResult> ExecuteAsync(TCommand command, StreamCommandDelegate<TResult> next, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            var cmd = (MiddlewareCommand)(object)command;
            cmd.ScopedDependencies.Add(scoped.Id);
            cmd.TransientDependencies.Add(transient.Id);
            cmd.Log += $"{Label}-in>";
            await foreach (var result in next().WithCancellation(ct))
                yield return result;
            cmd.Log += $"<{Label}-out";
        }
    }

    public class ClosedStreamMiddleware(ScopedDependency scoped, TransientDependency transient)
        : StreamMiddleware<StreamCommand, string>(scoped, transient)
    {
        protected override string Label => "closed";
    }
}
