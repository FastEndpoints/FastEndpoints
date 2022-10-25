using FakeItEasy;
using Microsoft.Extensions.Logging;
using TestCases.CommandBusTest;
using Web.Services;
using Xunit;

namespace FastEndpoints.UnitTests;

public class CommandBusTests
{
    [Fact]
    public async Task CommandExecusionWorks()
    {
        var command = new TestCommand { FirstName = "a", LastName = "b" };
        var handler = new TestCommandHandler(A.Fake<ILogger<TestCommandHandler>>(), A.Fake<IEmailService>());

        var res = await handler.ExecuteAsync(command, default);

        res.Should().Be("a b");
    }
}
