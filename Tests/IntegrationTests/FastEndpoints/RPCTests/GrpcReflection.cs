using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1Alpha;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;

namespace RemoteProcedureCalls;

public class GrpcReflection(ITestOutputHelper output)
{
    [Fact]
    public async Task Reflection_Lists_And_Describes_A_Command_Handler()
    {
        await using var server = await StartServerAsync();
        var client = ReflectionClient(server);

        var listed = await SingleAsync(client, new() { ListServices = "" });
        var services = listed.ListServicesResponse.Service.Select(s => s.Name).ToList();

        foreach (var s in services)
            output.WriteLine(s);

        //the handler is discoverable under the very service name it's bound to
        services.ShouldContain(typeof(ReflectedCommand).FullName!);

        //...and describable: grpcurl's `describe` gets a real FileDescriptorProto back
        var described = await SingleAsync(client, new() { FileContainingSymbol = typeof(ReflectedCommand).FullName! });
        described.MessageResponseCase.ShouldBe(ServerReflectionResponse.MessageResponseOneofCase.FileDescriptorResponse);
        described.FileDescriptorResponse.FileDescriptorProto.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Protobuf_Wire_Format_Executes_A_Command()
    {
        await using var server = await StartServerAsync();

        var svcProvider = new ServiceCollection()
                          .AddSingleton<ILoggerFactory, LoggerFactory>()
                          .AddSingleton(typeof(ILogger<>), typeof(Logger<>))
                          .BuildServiceProvider();

        var remote = new RemoteConnection("http://testhost", svcProvider) //hostname is irrelevant - the test handler below does the transport
        {
            MarshallerFactory = new ProtobufMarshallerFactory() //must be set before Register()
        };
        remote.ChannelOptions.HttpHandler = server.GetTestServer().CreateHandler();
        remote.Register<ReflectedCommand, ReflectedResult>();

        //the schema published by reflection is generated from this very marshaller's model, so the wire matches the descriptor
        var result = await new ReflectedCommand { FirstName = "johnny", LastName = "lawrence" }.RemoteExecuteAsync();

        result.FullName.ShouldBe("johnny lawrence");
    }

    [Fact]
    public void Generates_Descriptors_From_Command_Types()
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(typeof(ReflectedCommand).FullName!, MethodType.Unary, typeof(ReflectedCommand), typeof(ReflectedResult));

        var descriptors = CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory());

        var svc = descriptors.ShouldHaveSingleItem();
        svc.FullName.ShouldBe(typeof(ReflectedCommand).FullName); //the service name FE binds the handler under

        var method = svc.Methods.ShouldHaveSingleItem();

        //the descriptor describes the method that's actually bound - both read the name off the same factory,
        //so a grpcurl `describe` and a grpcurl `invoke` line up
        method.Name.ShouldBe(new ProtobufMarshallerFactory().MethodName);

        //no attributes on the command: public props, alphabetical, numbered from 1
        method.InputType.Fields.InFieldNumberOrder().Select(f => f.Name).ShouldBe(["FirstName", "LastName"]);
        method.OutputType.Fields.InFieldNumberOrder().Select(f => f.Name).ShouldBe(["FullName"]);
    }

    [Fact]
    public void Reflection_Needs_The_Protobuf_Wire_Format()
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(typeof(ReflectedCommand).FullName!, MethodType.Unary, typeof(ReflectedCommand), typeof(ReflectedResult));

        //messagepack has no protobuf descriptors to publish, so this fails loudly instead of serving a schema that lies
        Should.Throw<InvalidOperationException>(() => CommandDescriptorFactory.Build(registry, MessagePackMarshallerFactory.Instance))
              .Message.ShouldContain(nameof(ProtobufMarshallerFactory));
    }

    static async Task<WebApplication> StartServerAsync()
    {
        var bld = WebApplication.CreateBuilder();
        bld.WebHost.UseTestServer(); //in-memory transport, no ports
        bld.Services.AddHandlerServer(marshaller: new ProtobufMarshallerFactory());
        bld.Services.AddHandlerReflection();

        var app = bld.Build();
        app.MapHandlers(h => h.Register<ReflectedCommand, ReflectedCommandHandler, ReflectedResult>());
        app.MapHandlerReflection();
        await app.StartAsync();

        return app;
    }

    static ServerReflection.ServerReflectionClient ReflectionClient(WebApplication server)
    {
        var channel = GrpcChannel.ForAddress(
            "http://localhost", //ignored - the in-memory test handler does the transport
            new GrpcChannelOptions { HttpHandler = server.GetTestServer().CreateHandler() });

        return new ServerReflection.ServerReflectionClient(channel);
    }

    static async Task<ServerReflectionResponse> SingleAsync(ServerReflection.ServerReflectionClient client, ServerReflectionRequest req)
    {
        using var call = client.ServerReflectionInfo();
        await call.RequestStream.WriteAsync(req);
        await call.RequestStream.CompleteAsync();
        await call.ResponseStream.MoveNext(default);

        return call.ResponseStream.Current;
    }
}

//an ordinary attribute-free FE command - the point of the feature is that types like these need no protobuf annotations
public class ReflectedCommand : ICommand<ReflectedResult>
{
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
}

public class ReflectedResult
{
    public string FullName { get; set; } = "";
}

public class ReflectedCommandHandler : ICommandHandler<ReflectedCommand, ReflectedResult>
{
    public Task<ReflectedResult> ExecuteAsync(ReflectedCommand cmd, CancellationToken ct)
        => Task.FromResult(new ReflectedResult { FullName = $"{cmd.FirstName} {cmd.LastName}" });
}
