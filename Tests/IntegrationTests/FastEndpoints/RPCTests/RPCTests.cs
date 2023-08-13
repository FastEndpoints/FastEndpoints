using FastEndpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Shared;
using TestCases.ClientStreamingTest;
using TestCases.CommandBusTest;
using TestCases.EventQueueTest;
using TestCases.ServerStreamingTest;

namespace RemoteProcedureCalls;

public class RPCTestBase : TestBase
{
    protected readonly RemoteConnection remote;

    public RPCTestBase(WebFixture fixture) : base(fixture)
    {
        var svcCollection = new ServiceCollection();
        svcCollection.AddSingleton<ILoggerFactory, LoggerFactory>();
        svcCollection.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var svcProvider = svcCollection.BuildServiceProvider();
        remote = new RemoteConnection("http://testhost", svcProvider); //the actual hostname doesn't matter as we're replacing the httphandler below
        remote.ChannelOptions.HttpHandler = Web.CreateHttpMessageHandler();
        remote.Register<TestCases.CommandBusTest.VoidCommand>();
        remote.Register<SomeCommand, string>();
        remote.Register<EchoCommand, EchoCommand>();
        remote.RegisterServerStream<StatusStreamCommand, StatusUpdate>();
        remote.RegisterClientStream<CurrentPosition, ProgressReport>();
        remote.Subscribe<TestEvent, TestEventHandler>();
        Thread.Sleep(500);
    }
}