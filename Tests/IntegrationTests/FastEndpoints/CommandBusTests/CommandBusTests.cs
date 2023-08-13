using FastEndpoints;
using Shared;
using TestCases.CommandBusTest;
using Xunit;

namespace CommandBus;

public class CommandBusTests : TestBase
{
    public CommandBusTests(WebFixture fixture) : base(fixture) { }

    [Fact]
    public async Task Command_Handler_Sends_Error_Response()
    {
        var res = await Web.GuestClient.GETAsync<TestCases.CommandHandlerTest.Endpoint, ErrorResponse>();
        res.Response.IsSuccessStatusCode.Should().BeFalse();
        res.Result!.StatusCode.Should().Be(400);
        res.Result.Errors.Count.Should().Be(2);
        res.Result.Errors["GeneralErrors"].Count.Should().Be(2);
    }

    [Fact]
    public async Task Command_That_Returns_A_Result()
    {
        var res1 = await new TestCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        }
        .ExecuteAsync();

        var res2 = await new TestCommand
        {
            FirstName = "jo",
            LastName = "law"
        }
        .ExecuteAsync();

        res1.Should().Be("johnny lawrence");
        res2.Should().Be("jo law");
    }

    [Fact]
    public async Task Command_That_Returns_A_Result_With_TestHandler()
    {
        var client = Web.CreateClient(s =>
        {
            s.RegisterTestCommandHandler<TestCommand, TestCommandHandler, string>();
        });

        var (rsp, res) = await client.GETAsync<Endpoint, string>();

        rsp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        TestCommandHandler.FullName.Should().Be("x y zseeeee!");
    }

    [Fact]
    public async Task Void_Command_With_Original_Handler()
    {
        var cmd = new TestVoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        await cmd.ExecuteAsync();

        cmd.FirstName.Should().Be("pass");
        cmd.LastName.Should().Be("pass");
    }

    [Fact]
    public async Task Void_Command_With_Test_Handler()
    {
        var client = Web.CreateClient(s =>
        {
            s.RegisterTestCommandHandler<TestVoidCommand, TestVoidCommandHandler>();
        });

        var (rsp, res) = await client.GETAsync<Endpoint, string>();

        rsp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        TestVoidCommandHandler.FullName.Should().Be("x y z");
    }

    [Fact]
    public async Task Non_Concrete_Void_Command()
    {
        ICommand cmd = new TestVoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var act = async () => cmd.ExecuteAsync();

        await act.Should().NotThrowAsync();
    }
}

[DontRegister]
sealed class TestVoidCommandHandler : ICommandHandler<TestVoidCommand>
{
    public static string FullName = default!;

    public Task ExecuteAsync(TestVoidCommand command, CancellationToken ct)
    {
        FullName = command.FirstName + " " + command.LastName + " z";
        return Task.CompletedTask;
    }
}

[DontRegister]
sealed class TestCommandHandler : ICommandHandler<TestCommand, string>
{
    public static string FullName = default!;

    public Task<string> ExecuteAsync(TestCommand command, CancellationToken c)
    {
        FullName = command.FirstName + " " + command.LastName + " zseeeee!";
        return Task.FromResult(FullName);
    }
}