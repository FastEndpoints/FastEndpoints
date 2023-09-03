using TestCases.CommandBusTest;

namespace CommandBus;

public class CommandBusTests : TestClass<Fixture>
{
    public CommandBusTests(Fixture f, ITestOutputHelper o) : base(f, o) { }

    [Fact]
    public async Task Command_Handler_Sends_Error_Response()
    {
        var res = await Fixture.Client.GETAsync<TestCases.CommandHandlerTest.Endpoint, ErrorResponse>();
        res.Response.IsSuccessStatusCode.Should().BeFalse();
        res.Result!.StatusCode.Should().Be(400);
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

    [Fact]
    public async Task Command_That_Returns_A_Result_With_TestHandler()
    {
        var (rsp, _) = await Fixture.Client.GETAsync<Endpoint, string>();

        rsp.StatusCode.Should().Be(System.Net.HttpStatusCode.OK);
        TestCommandHandler.FullName.Should().Be("x y zseeeee!");
    }

    [Fact]
    public async Task Void_Command_With_Test_Handler()
    {
        var (rsp, _) = await Fixture.Client.GETAsync<Endpoint, string>();

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