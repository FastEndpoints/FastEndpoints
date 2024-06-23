using TestCases.CommandBusTest;

namespace Messaging;

public class CommandBusTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task Generic_Command_With_Result()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<TestCases.CommandHandlerTest.GenericCmdEndpoint, IEnumerable<Guid>>();
        rsp.IsSuccessStatusCode.Should().BeTrue();
        res.Count().Should().Be(3);
        res.First().Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Generic_Command_Without_Result()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<TestCases.CommandHandlerTest.GenericCmdWithoutResultEndpoint, Guid>();
        rsp.IsSuccessStatusCode.Should().BeTrue();
        res.Should().Be(Guid.Empty);
    }

    [Fact]
    public async Task Command_Handler_Sends_Error_Response()
    {
        var res = await App.Client.GETAsync<TestCases.CommandHandlerTest.ConcreteCmdEndpoint, ErrorResponse>();
        res.Response.IsSuccessStatusCode.Should().BeFalse();
        res.Result.StatusCode.Should().Be(400);
        res.Result.Errors.Count.Should().Be(2);
        res.Result.Errors["GeneralErrors"].Count.Should().Be(2);
    }

    [Fact]
    public async Task Non_Concrete_Void_Command()
    {
        ICommand cmd = new VoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var act = async () => cmd.ExecuteAsync();

        await act.Should().NotThrowAsync();
    }

    [Fact, Trait("ExcludeInCiCd", "Yes")]
    public async Task Command_That_Returns_A_Result_With_TestHandler()
    {
        var (rsp, _) = await App.Client.GETAsync<Endpoint, string>();
        rsp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        TestCommandHandler.FullName.Should().Be("x y zseeeee!");
    }

    [Fact]
    public async Task Void_Command_With_Test_Handler()
    {
        var (rsp, _) = await App.Client.GETAsync<Endpoint, string>();

        rsp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        TestVoidCommandHandler.FullName.Should().Be("x y z");
    }
}

[DontRegister]
public class TestVoidCommandHandler : ICommandHandler<VoidCommand>
{
    public static string FullName = default!;

    public Task ExecuteAsync(VoidCommand command, CancellationToken ct)
    {
        FullName = command.FirstName + " " + command.LastName + " z";

        return Task.CompletedTask;
    }
}

[DontRegister]
public class TestCommandHandler : ICommandHandler<SomeCommand, string>
{
    public static string FullName = default!;

    public Task<string> ExecuteAsync(SomeCommand command, CancellationToken c)
    {
        FullName = command.FirstName + " " + command.LastName + " zseeeee!";

        return Task.FromResult(FullName);
    }
}