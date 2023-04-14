using FakeItEasy;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using TestCases.CommandBusTest;
using TestCases.CommandHandlerTest;
using Web.Services;
using Xunit;

namespace FastEndpoints.UnitTests;

public class CommandBusTests
{
    [Fact]
    public async Task AbilityToFakeTheCommandHandler()
    {
        var command = new TestCommand { FirstName = "a", LastName = "b" };

        var fakeHandler = A.Fake<ICommandHandler<TestCommand, string>>();
        A.CallTo(() => fakeHandler.ExecuteAsync(A<TestCommand>.Ignored, A<CancellationToken>.Ignored))
            .Returns(Task.FromResult("Fake Result"));

        var handlersCacheContainer = A.Fake<IHandlersCacheContainer>();
        A.CallTo(() => handlersCacheContainer.HandlersCache).Returns(new Dictionary<Type, CommandHandlerDefinition> {
            {
                typeof(TestCommand), new(fakeHandler.GetType())
            }
        });

        var services = new ServiceCollection();
        services.TryAddSingleton(handlersCacheContainer);
        services.TryAddSingleton(fakeHandler.GetType(), _ => fakeHandler);
        services.TryAddSingleton<IServiceResolver>(sp => new ProxyServiceResolver(sp));
        var serviceProvider = services.BuildServiceProvider();
        
        var oldServiceResolver = FastEndpoints.Config.ServiceResolver; 
        FastEndpoints.Config.ServiceResolver = serviceProvider.GetRequiredService<IServiceResolver>();
        var result = await command.ExecuteAsync();
        FastEndpoints.Config.ServiceResolver = oldServiceResolver;
        
        Assert.Equal("Fake Result", result);
    }

    [Fact]
    public async Task CommandExecusionWorks()
    {
        var command = new TestCommand { FirstName = "a", LastName = "b" };
        var handler = new TestCommandHandler(A.Fake<ILogger<TestCommandHandler>>(), A.Fake<IEmailService>());

        var res = await handler.ExecuteAsync(command, default);

        res.Should().Be("a b");
    }

    [Fact]
    public async Task CommandHandlerAddsErrors()
    {
        var command = new GetFullName { FirstName = "yoda", LastName = "minch" };
        var handler = new MakeFullName(A.Fake<ILogger<MakeFullName>>());

        try
        {
            await handler.ExecuteAsync(command, default);
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
        var command = new GetFullName { FirstName = "bobbaa", LastName = "fett" };
        var handler = new MakeFullName(A.Fake<ILogger<MakeFullName>>());

        await handler.ExecuteAsync(command, default);

        handler.ValidationFailures.Should().HaveCount(0);
    }
}
