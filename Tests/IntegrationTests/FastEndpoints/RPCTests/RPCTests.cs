using FastEndpoints;
using Grpc.Core;
using Int.Shared.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TestCases.ClientStreamingTest;
using TestCases.CommandBusTest;
using TestCases.EventQueueTest;
using TestCases.ServerStreamingTest;
using Xunit;
using Xunit.Abstractions;

namespace Int.RPC;

public class RPCTests : EndToEndTestBase
{
    private readonly RemoteConnection remote;

    public RPCTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {
        var svcCollection = new ServiceCollection();
        svcCollection.AddSingleton<ILoggerFactory, LoggerFactory>();
        svcCollection.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var svcProvider = svcCollection.BuildServiceProvider();
        remote = new RemoteConnection("http://testhost", svcProvider); //the actual hostname doesn't matter as we're replacing the httphandler below
        remote.ChannelOptions.HttpHandler = endToEndTestFixture.CreateHttpMessageHandler();
        remote.Register<TestVoidCommand>();
        remote.Register<TestCommand, string>();
        remote.Register<EchoCommand, EchoCommand>();
        remote.RegisterServerStream<StatusStreamCommand, StatusUpdate>();
        remote.RegisterClientStream<CurrentPosition, ProgressReport>();
        remote.Subscribe<TestEvent, TestEventHandler>();
        Thread.Sleep(500);
    }

    [Fact]
    public async Task Void()
    {
        var command = new TestVoidCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };
        await remote.ExecuteVoid(command, command.GetType(), default);
    }

    [Fact]
    public async Task Unary()
    {
        var command = new TestCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var res = await remote.ExecuteUnary(command, command.GetType(), default);

        res.Should().Be("johnny lawrence");
    }

    [Fact]
    public async Task Unary_Echo()
    {
        var command = new EchoCommand
        {
            FirstName = "johnny",
            LastName = "lawrence"
        };

        var res = await remote.ExecuteUnary(command, command.GetType(), default);

        res.Should().BeEquivalentTo(command);
    }

    [Fact]
    public async Task Server_Stream()
    {
        var command = new StatusStreamCommand
        {
            Id = 101
        };

        var iterator = remote.ExecuteServerStream(command, command.GetType(), default).ReadAllAsync();

        var i = 1;
        await foreach (var status in iterator)
        {
            status.Message.Should().Be($"Id: {101} - {i}");
            i++;
            if (i == 10)
                break;
        }
    }

    [Fact]
    public async Task Client_Stream()
    {
        var input = GetDataStream();

        var report = await remote.ExecuteClientStream<CurrentPosition, ProgressReport>(
            input, typeof(IAsyncEnumerable<CurrentPosition>), default);

        report.LastNumber.Should().Be(5);

        static async IAsyncEnumerable<CurrentPosition> GetDataStream()
        {
            var i = 0;
            while (i < 5)
            {
                i++;
                yield return new() { Number = i };
            }
        }
    }

    [Fact]
    public async Task Event_Queue()
    {
        for (var i = 0; i < 100; i++)
        {
            var evnt = new TestEvent { Id = i };
            evnt.Broadcast();
            await Task.Delay(10);
        }
        TestEventHandler.Received.Count.Should().Be(100);
        TestEventHandler.Received.Select(r => r.Id).Except(Enumerable.Range(0, 100)).Any().Should().BeFalse();
    }
}