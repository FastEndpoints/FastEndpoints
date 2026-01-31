using TestCases.CommandBusTest;
using TestCases.CommandHandlerTest;

namespace Messaging;

public class CommandBusTests(Sut App) : TestBase<Sut>
{
    [Fact]
    public async Task Test_Command_Receiver_Receives_Executed_Command()
    {
        var name = Guid.NewGuid().ToString();

        var rsp = await App.GuestClient.GETAsync<ReceiverEndpoint, ReceiverRequest>(
                      new()
                      {
                          Name = name
                      });
        rsp.IsSuccessStatusCode.ShouldBeTrue();

        var receiver = App.Services.GetTestCommandReceiver<VoidCommand>();
        var received = await receiver.WaitForMatchAsync(v => v.FirstName == name && v.LastName == name);
        received.ShouldContain(v => v.FirstName == name);
    }

    [Fact]
    public async Task Generic_Command_With_Result()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<GenericCmdEndpoint, IEnumerable<Guid>>();
        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Count().ShouldBe(3);
        res.First().ShouldBe(Guid.Empty);
    }

    [Fact]
    public async Task Generic_Command_Without_Result()
    {
        var (rsp, res) = await App.GuestClient.GETAsync<GenericCmdWithoutResultEndpoint, Guid>();
        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.ShouldBe(Guid.Empty);
    }

    [Fact]
    public async Task Command_Handler_Sends_Error_Response()
    {
        var res = await App.Client.GETAsync<ConcreteCmdEndpoint, ErrorResponse>();
        res.Response.IsSuccessStatusCode.ShouldBeFalse();
        res.Result.StatusCode.ShouldBe(400);
        res.Result.Errors.Count.ShouldBe(2);
        res.Result.Errors["generalErrors"].Count.ShouldBe(2);
    }

    [Fact]
    public async Task Non_Concrete_Void_Command()
    {
        ICommand cmd = new VoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var act = async () => cmd.ExecuteAsync(Cancellation);

        await act.ShouldNotThrowAsync();
    }

    [Fact, Trait("ExcludeInCiCd", "Yes")]
    public async Task Command_That_Returns_A_Result_With_TestHandler()
    {
        var (rsp, _) = await App.Client.GETAsync<Endpoint, string>();
        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        TestCommandHandler.FullName.ShouldBe("x y zseeeee!");
    }

    [Fact]
    public async Task Void_Command_With_Test_Handler()
    {
        var (rsp, _) = await App.Client.GETAsync<Endpoint, string>();

        rsp.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        TestVoidCommandHandler.FullName.ShouldBe("x y z");
    }

    [Fact]
    public async Task Command_Handler_Instances_Are_Unique()
    {
        var (rsp, res) = await App.Client.GETAsync<GetCommandHandlerHashCodes, IEnumerable<int>>();

        rsp.IsSuccessStatusCode.ShouldBeTrue();
        res.Distinct().Count().ShouldBe(res.Count()); // no duplicate hash codes
    }

    [Fact]
    public async Task Scoped_Service_Instances_Are_Unique_To_Each_Request()
    {
        List<Guid> svcInstanceIds = [];

        await Parallel.ForEachAsync(
            Enumerable.Range(1, 100),
            async (_, _) =>
            {
                var (rsp, res) = await App.Client.GETAsync<ScopedServiceCheckEndpoint, Guid>();
                rsp.IsSuccessStatusCode.ShouldBeTrue();
                res.ShouldNotBe(Guid.Empty);
                svcInstanceIds.Add(res);
            });

        svcInstanceIds.Distinct().Count().ShouldBe(svcInstanceIds.Count); // no duplicate service instances
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