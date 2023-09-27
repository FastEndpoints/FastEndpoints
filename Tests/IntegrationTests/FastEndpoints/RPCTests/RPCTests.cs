using Microsoft.Extensions.Logging;
using TestCases.ClientStreamingTest;
using TestCases.CommandBusTest;
using TestCases.EventQueueTest;
using TestCases.ServerStreamingTest;

namespace RemoteProcedureCalls;

public class RPCTestBase : TestClass<Fixture>
{
    protected readonly RemoteConnection remote;

    public RPCTestBase(Fixture f, ITestOutputHelper o) : base(f, o)
    {
        var svcCollection = new ServiceCollection();
        svcCollection.AddSingleton<ILoggerFactory, LoggerFactory>();
        svcCollection.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var svcProvider = svcCollection.BuildServiceProvider();
        remote = new RemoteConnection("http://testhost", svcProvider); //the actual hostname doesn't matter as we're replacing the httphandler below
        remote.ChannelOptions.HttpHandler = Fixture.CreateHandler();
        remote.Register<TestCases.CommandBusTest.VoidCommand>();
        remote.Register<SomeCommand, string>();
        remote.Register<EchoCommand, EchoCommand>();
        remote.RegisterServerStream<StatusStreamCommand, StatusUpdate>();
        remote.RegisterClientStream<CurrentPosition, ProgressReport>();
        remote.Subscribe<TestEventQueue, TestEventQueueHandler>();
        Thread.Sleep(500);
    }
}