using System.Buffers;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1Alpha;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

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

        var descriptors = CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory(), NullLogger.Instance);

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

    //shapes that a hand-rolled CLR->proto mapping gets wrong. each of these was a confirmed failure before being fixed.
    [Fact]
    public void Describes_Awkward_Property_Shapes()
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(typeof(AwkwardCommand).FullName!, MethodType.Unary, typeof(AwkwardCommand), typeof(ReflectedResult));

        var svc = CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory(), NullLogger.Instance).ShouldHaveSingleItem();
        var fields = svc.Methods.ShouldHaveSingleItem().InputType.Fields.InFieldNumberOrder().ToList();

        //a nullable struct used to blow up descriptor generation AND corrupt the wire (Nullable<T> was never registered)
        fields.Single(f => f.Name == nameof(AwkwardCommand.Location)).FieldType.ShouldBe(Google.Protobuf.Reflection.FieldType.Message);

        //a generic member's CLR name carries an arity tick, which is not a legal proto identifier
        fields.Single(f => f.Name == nameof(AwkwardCommand.Envelope)).FieldType.ShouldBe(Google.Protobuf.Reflection.FieldType.Message);

        //collections must be described as repeated, not as a single message
        fields.Single(f => f.Name == nameof(AwkwardCommand.Tags)).IsRepeated.ShouldBeTrue();
    }

    //two types with the same simple name in different namespaces used to collide on one proto symbol
    [Fact]
    public void Describes_Same_Named_Types_From_Different_Namespaces()
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(typeof(CollidingCommand).FullName!, MethodType.Unary, typeof(CollidingCommand), typeof(ReflectedResult));

        var svc = CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory(), NullLogger.Instance).ShouldHaveSingleItem();
        var fields = svc.Methods.ShouldHaveSingleItem().InputType.Fields.InFieldNumberOrder().ToList();

        fields.Select(f => f.Name).ShouldBe([nameof(CollidingCommand.Billing), nameof(CollidingCommand.Shipping)]);
        fields[0].MessageType.FullName.ShouldNotBe(fields[1].MessageType.FullName); //distinct symbols, not one shadowing the other
    }

    //a scalar has no top-level protobuf message. this is what an event hub's subscriber id and ICommand<string> hit.
    [Fact]
    public void Wraps_Scalar_Payloads_Instead_Of_Throwing()
    {
        var marshaller = new ProtobufMarshallerFactory().Create<string>();

        RoundTrip(marshaller, "johnny").ShouldBe("johnny");
    }

    static T RoundTrip<T>(Marshaller<T> m, T value)
    {
        var buffer = new ArrayBufferWriter<byte>();
        var sc = new FakeSerializationContext(buffer);
        m.ContextualSerializer(value, sc);

        return m.ContextualDeserializer(new FakeDeserializationContext(new(buffer.WrittenMemory)));
    }

    sealed class FakeSerializationContext(IBufferWriter<byte> writer) : SerializationContext
    {
        public override IBufferWriter<byte> GetBufferWriter() => writer;
        public override void Complete() { }
        public override void Complete(byte[] payload) { }
    }

    sealed class FakeDeserializationContext(ReadOnlySequence<byte> payload) : DeserializationContext
    {
        public override int PayloadLength => (int)payload.Length;
        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence() => payload;
        public override byte[] PayloadAsNewBuffer() => payload.ToArray();
    }

    //a command that can't be described must not take every other handler's schema down with it. all descriptors used to be
    //built into one pool, so a single unmapped property type killed reflection for the whole server.
    [Fact]
    public void An_Undescribable_Command_Is_Skipped_Not_Fatal()
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(typeof(ReflectedCommand).FullName!, MethodType.Unary, typeof(ReflectedCommand), typeof(ReflectedResult));
        registry.Add(typeof(UndescribableCommand).FullName!, MethodType.Unary, typeof(UndescribableCommand), typeof(ReflectedResult));

        var descriptors = CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory(), NullLogger.Instance);

        descriptors.ShouldHaveSingleItem().FullName.ShouldBe(typeof(ReflectedCommand).FullName);
    }

    [Fact]
    public void Reflection_Needs_The_Protobuf_Wire_Format()
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(typeof(ReflectedCommand).FullName!, MethodType.Unary, typeof(ReflectedCommand), typeof(ReflectedResult));

        //messagepack has no protobuf descriptors to publish, so this fails loudly instead of serving a schema that lies
        Should.Throw<InvalidOperationException>(() => CommandDescriptorFactory.Build(registry, MessagePackMarshallerFactory.Instance, NullLogger.Instance))
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

public class AwkwardCommand : ICommand<ReflectedResult>
{
    public Envelope<string>? Envelope { get; set; }
    public Point? Location { get; set; }
    public List<string> Tags { get; set; } = [];
}

public class Envelope<T>
{
    public T? Body { get; set; }
}

public struct Point
{
    public int X { get; set; }
    public int Y { get; set; }
}

public class CollidingCommand : ICommand<ReflectedResult>
{
    public Billing.Address? Billing { get; set; }
    public Shipping.Address? Shipping { get; set; }
}

//protobuf-net serializes DateTime as its own bcl message, which isn't described yet - this handler still executes fine
public class UndescribableCommand : ICommand<ReflectedResult>
{
    public DateTime PlacedOn { get; set; }
}

public class ReflectedCommandHandler : ICommandHandler<ReflectedCommand, ReflectedResult>
{
    public Task<ReflectedResult> ExecuteAsync(ReflectedCommand cmd, CancellationToken ct)
        => Task.FromResult(new ReflectedResult { FullName = $"{cmd.FirstName} {cmd.LastName}" });
}
