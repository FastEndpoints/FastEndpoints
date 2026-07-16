using System.Buffers;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Reflection.V1Alpha;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace RemoteProcedureCalls;

public class GrpcReflection
{
    [Fact]
    public async Task Reflection_Lists_And_Describes_A_Command_Handler()
    {
        await using var server = await StartServerAsync();
        var client = ReflectionClient(server);

        var listed = await SingleAsync(client, new() { ListServices = "" });
        var services = listed.ListServicesResponse.Service.Select(s => s.Name).ToList();

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

        //the descriptor must describe the method that's actually bound, so a grpcurl `describe` and `invoke` line up.
        //asserted against the literal rather than the factory, which would just be the generator agreeing with itself.
        method.Name.ShouldBe("Execute");

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

    //the default wire format must keep binding commands under an empty method name, or every existing client breaks.
    //this is the wire-compat guarantee that makes the protobuf method name safe to introduce at all.
    [Fact]
    public void Default_Wire_Format_Still_Binds_An_Empty_Method_Name()
        => ((IRpcMarshallerFactory)MessagePackMarshallerFactory.Instance).MethodName.ShouldBe("");

    //every marshaller EventHub.Bind asks for. the subscriber id used to throw here, which stopped the server booting
    //outright for any app that had a hub. driven through the marshaller rather than a live hub on purpose: EventHub's
    //ctor writes to EventHubStorage<,>.Provider, a static shared by every hub using the same storage types, so standing
    //one up in-process trashes the shared test harness's provider and fails ~70 unrelated tests at random.
    [Fact]
    public void Marshals_Every_Payload_An_Event_Hub_Binds()
    {
        var factory = new ProtobufMarshallerFactory();

        RoundTrip(factory.Create<string>(), "subscriber-1").ShouldBe("subscriber-1");                 //the "sub" request
        RoundTrip(factory.Create<ReflectedEvent>(), new() { Message = "hi" }).Message.ShouldBe("hi"); //the streamed event
        RoundTrip(factory.Create<EmptyObject>(), EmptyObject.Instance).ShouldNotBeNull();             //the "pub" reply, a zero-field message
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
        public override IBufferWriter<byte> GetBufferWriter()
            => writer;

        public override void Complete() { }
        public override void Complete(byte[] payload) { }
    }

    sealed class FakeDeserializationContext(ReadOnlySequence<byte> payload) : DeserializationContext
    {
        public override int PayloadLength => (int)payload.Length;

        public override ReadOnlySequence<byte> PayloadAsReadOnlySequence()
            => payload;

        public override byte[] PayloadAsNewBuffer()
            => payload.ToArray();
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

    //ICommand<string> travels inside the Scalar<T> wrapper, so the descriptor has to publish that same wrapper.
    //it used to be dropped from the listing entirely: the model has no entry for a scalar, so building the file threw.
    [Fact]
    public void Describes_A_Command_With_A_Scalar_Result()
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(typeof(ScalarResultCommand).FullName!, MethodType.Unary, typeof(ScalarResultCommand), typeof(string));

        var svc = CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory(), NullLogger.Instance).ShouldHaveSingleItem();
        var output = svc.Methods.ShouldHaveSingleItem().OutputType;

        //matches what ScalarMarshaller actually writes: a one-field wrapper
        output.Fields.InFieldNumberOrder().Select(f => f.Name).ShouldBe(["Value"]);
    }

    //the generated message names must not carry the assembly version, or a version bump renames every generic symbol
    //and breaks already-generated clients
    [Fact]
    public void Generic_Message_Names_Are_Version_Independent()
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(typeof(AwkwardCommand).FullName!, MethodType.Unary, typeof(AwkwardCommand), typeof(ReflectedResult));

        var svc = CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory(), NullLogger.Instance).ShouldHaveSingleItem();
        var envelope = svc.Methods.ShouldHaveSingleItem().InputType.Fields.InFieldNumberOrder().Single(f => f.Name == nameof(AwkwardCommand.Envelope));

        envelope.MessageType.FullName.ShouldNotContain("Version");
        envelope.MessageType.FullName.ShouldNotContain("PublicKeyToken");
    }

    //shapes the wire handles but the descriptor can't map. they must be skipped with a warning, never mis-described:
    //a dictionary used to be published as a "repeated empty message" while the wire carried real entries.
    [Theory, InlineData(typeof(DictionaryCommand)), InlineData(typeof(Outer.NestedCommand))]
    public void Undescribable_Shapes_Are_Skipped_Rather_Than_Mis_Described(Type tCommand)
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(tCommand.FullName!, MethodType.Unary, tCommand, typeof(ReflectedResult));

        CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory(), NullLogger.Instance).ShouldBeEmpty();
    }

    //the descriptor has to describe the streaming shape the handler is actually bound with, or grpcurl and every generated
    //client get the call semantics wrong. nothing asserted these flags, so they could have been inverted unnoticed.
    [Theory, InlineData(MethodType.Unary, false, false), InlineData(MethodType.ServerStreaming, false, true), InlineData(MethodType.ClientStreaming, true, false),
     InlineData(MethodType.DuplexStreaming, true, true)]
    public void Describes_The_Streaming_Shape_Of_A_Handler(MethodType methodType, bool clientStreaming, bool serverStreaming)
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(typeof(ReflectedCommand).FullName!, methodType, typeof(ReflectedCommand), typeof(ReflectedResult));

        var method = CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory(), NullLogger.Instance)
                                             .ShouldHaveSingleItem()
                                             .Methods.ShouldHaveSingleItem();

        method.IsClientStreaming.ShouldBe(clientStreaming);
        method.IsServerStreaming.ShouldBe(serverStreaming);
    }

    //ICommand<List<Dto>> is wrapped like any other scalar, but the model still has to learn the element type or every
    //call fails at runtime with "no serializer for Dto"
    [Fact]
    public void Marshals_A_Collection_Payload()
    {
        var marshaller = new ProtobufMarshallerFactory().Create<List<ReflectedResult>>();

        RoundTrip(marshaller, [new() { FullName = "johnny" }]).ShouldHaveSingleItem().FullName.ShouldBe("johnny");
    }

    //Dictionary<K, message> used to fail at Create: MemberType is KeyValuePair<K,V>, IsMessage(KVP) is false by design
    //(descriptor path), and CanSerialize(KVP) is false until V is registered - but Register never walked into map values.
    [Fact]
    public void Marshals_A_Dictionary_Of_Messages()
    {
        var factory = new ProtobufMarshallerFactory();

        //bind order is Create<TCommand>() then Create<TResult>() - command registration must not depend on result first
        var commandMarshaller = factory.Create<MapOfMessagesCommand>();
        _ = factory.Create<ReflectedResult>();

        var back = RoundTrip(
            commandMarshaller,
            new MapOfMessagesCommand
            {
                Items =
                {
                    ["a"] = new() { FullName = "alice" },
                    ["b"] = new() { FullName = "bob" }
                },
                Nested =
                {
                    [1] = new() { Tags = ["x", "y"], Body = new() { FullName = "nested" } }
                }
            });

        back.Items.Count.ShouldBe(2);
        back.Items["a"].FullName.ShouldBe("alice");
        back.Items["b"].FullName.ShouldBe("bob");
        back.Nested.ShouldHaveSingleItem().Value.Tags.ShouldBe(["x", "y"]);
        back.Nested[1].Body!.FullName.ShouldBe("nested");
    }

    //scalar maps already worked; keep that path covered so the KeyValuePair special-case does not regress them
    [Fact]
    public void Marshals_A_Dictionary_Of_Scalars()
    {
        var marshaller = new ProtobufMarshallerFactory().Create<DictionaryCommand>();

        var back = RoundTrip(marshaller, new DictionaryCommand { Meta = { ["k"] = "v", ["n"] = "1" } });

        back.Meta.Count.ShouldBe(2);
        back.Meta["k"].ShouldBe("v");
        back.Meta["n"].ShouldBe("1");
    }

    //unsupported map *value* types must still fail at Create with the same startup-time NotSupportedException
    [Fact]
    public void Unsupported_Dictionary_Value_Types_Fail_At_Marshaller_Creation()
    {
        Should.Throw<NotSupportedException>(() => new ProtobufMarshallerFactory().Create<MapOfUnsupportedCommand>())
              .Message.ShouldContain("cannot serialize property type");
    }

    //BCL types protobuf-net already serializes must round-trip through the attribute-free model - registering them as
    //empty messages used to either silent-drop (DateTimeOffset) or blow up at Add (DateOnly/TimeOnly/Uri).
    [Fact]
    public void Marshals_Supported_Bcl_Property_Types()
    {
        var marshaller = new ProtobufMarshallerFactory().Create<SupportedBclCommand>();
        var original = new SupportedBclCommand
        {
            Day = new(2024, 6, 15),
            PlacedAt = new(2024, 6, 15, 12, 30, 0, DateTimeKind.Utc),
            Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            Link = new("https://example.com/x"),
            Amount = 123.45m,
            Duration = TimeSpan.FromHours(1.5),
            Time = new(12, 30, 0)
        };

        var back = RoundTrip(marshaller, original);

        back.Day.ShouldBe(original.Day);
        back.PlacedAt.ShouldBe(original.PlacedAt);
        back.Id.ShouldBe(original.Id);
        back.Link.ShouldBe(original.Link);
        back.Amount.ShouldBe(original.Amount);
        back.Duration.ShouldBe(original.Duration);
        back.Time.ShouldBe(original.Time);
    }

    //System.Type is denylisted as a non-message so protobuf-net's assembly-qualified-name serializer is used rather than
    //a hollow attribute-free contract. Create must succeed and the value must round-trip.
    [Fact]
    public void Marshals_System_Type_Property()
    {
        var marshaller = new ProtobufMarshallerFactory().Create<TypeCommand>();
        var original = new TypeCommand { Kind = typeof(string) };

        RoundTrip(marshaller, original).Kind.ShouldBe(typeof(string));
    }

    //DateTimeOffset/JsonElement/StringBuilder (and similar BCL types with no useful public r/w payload props) used to be
    //registered as empty/hollow messages: wire 0A00 or metadata-only, deserialize default/corrupted, no error. fail at
    //Create/Register instead so FE↔FE traffic cannot silently drop data.
    [Theory]
    [InlineData(typeof(DateTimeOffsetCommand))]
    [InlineData(typeof(HalfCommand))]
    [InlineData(typeof(Int128Command))]
    [InlineData(typeof(UInt128Command))]
    [InlineData(typeof(VersionCommand))]
    [InlineData(typeof(JsonElementCommand))]
    [InlineData(typeof(StringBuilderCommand))]
    [InlineData(typeof(TimeZoneInfoCommand))]
    [InlineData(typeof(IPAddressCommand))]
    [InlineData(typeof(ExceptionCommand))]
    [InlineData(typeof(MemoryStreamCommand))]
    public void Unsupported_Bcl_Property_Types_Fail_At_Marshaller_Creation(Type commandType)
    {
        var create = typeof(ProtobufMarshallerFactory).GetMethod(nameof(ProtobufMarshallerFactory.Create))!
                                                      .MakeGenericMethod(commandType);

        Should.Throw<TargetInvocationException>(() => create.Invoke(new ProtobufMarshallerFactory(), null))
              .InnerException.ShouldBeOfType<NotSupportedException>()
              .Message.ShouldContain("cannot serialize property type");
    }

    //same denylist drives descriptors: these must be skipped with CommandNotDescribable, never published as empty nested messages.
    [Theory]
    [InlineData(typeof(DateTimeOffsetCommand))]
    [InlineData(typeof(HalfCommand))]
    [InlineData(typeof(Int128Command))]
    [InlineData(typeof(UInt128Command))]
    [InlineData(typeof(VersionCommand))]
    [InlineData(typeof(JsonElementCommand))]
    [InlineData(typeof(StringBuilderCommand))]
    [InlineData(typeof(TimeZoneInfoCommand))]
    [InlineData(typeof(IPAddressCommand))]
    [InlineData(typeof(ExceptionCommand))]
    [InlineData(typeof(MemoryStreamCommand))]
    [InlineData(typeof(TypeCommand))]
    [InlineData(typeof(SupportedBclCommand))] //DateTime/DateOnly/etc. still undescribable even though the wire works
    public void Unsupported_Bcl_Shapes_Are_Skipped_Rather_Than_Mis_Described(Type tCommand)
    {
        var registry = new RpcSchemaRegistry();
        registry.Add(tCommand.FullName!, MethodType.Unary, tCommand, typeof(ReflectedResult));

        CommandDescriptorFactory.Build(registry, new ProtobufMarshallerFactory(), NullLogger.Instance).ShouldBeEmpty();
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
            new() { HttpHandler = server.GetTestServer().CreateHandler() });

        return new(channel);
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

//wire works via protobuf-net inbuilts; descriptors still skip (bcl.* / non-proto3 scalars not published yet)
public class SupportedBclCommand : ICommand<ReflectedResult>
{
    public decimal Amount { get; set; }
    public DateOnly Day { get; set; }
    public TimeSpan Duration { get; set; }
    public Guid Id { get; set; }
    public Uri? Link { get; set; }
    public DateTime PlacedAt { get; set; }
    public TimeOnly Time { get; set; }
}

//these used to silent-drop as empty messages; marshaller Create must throw instead
public class DateTimeOffsetCommand : ICommand<ReflectedResult>
{
    public DateTimeOffset When { get; set; }
}

public class HalfCommand : ICommand<ReflectedResult>
{
    public Half Value { get; set; }
}

public class Int128Command : ICommand<ReflectedResult>
{
    public Int128 Value { get; set; }
}

public class UInt128Command : ICommand<ReflectedResult>
{
    public UInt128 Value { get; set; }
}

public class VersionCommand : ICommand<ReflectedResult>
{
    public Version? Value { get; set; }
}

//open JSON / builder / framework shapes that used to hollow-register (empty or metadata-only contracts)
public class JsonElementCommand : ICommand<ReflectedResult>
{
    public JsonElement Payload { get; set; }
}

public class StringBuilderCommand : ICommand<ReflectedResult>
{
    public StringBuilder? Text { get; set; }
}

public class TimeZoneInfoCommand : ICommand<ReflectedResult>
{
    public TimeZoneInfo? Zone { get; set; }
}

public class IPAddressCommand : ICommand<ReflectedResult>
{
    public IPAddress? Addr { get; set; }
}

public class ExceptionCommand : ICommand<ReflectedResult>
{
    public Exception? Error { get; set; }
}

public class MemoryStreamCommand : ICommand<ReflectedResult>
{
    public MemoryStream? Body { get; set; }
}

public class TypeCommand : ICommand<ReflectedResult>
{
    public Type? Kind { get; set; }
}

public class ScalarResultCommand : ICommand<string>
{
    public string Name { get; set; } = "";
}

//a dictionary round-trips fine on the wire, but KeyValuePair's Key/Value are getter-only so it can't be described
public class DictionaryCommand : ICommand<ReflectedResult>
{
    public Dictionary<string, string> Meta { get; set; } = [];
}

//message-valued maps must register the value type during Create - used to throw NotSupportedException on KeyValuePair<,>
public class MapOfMessagesCommand : ICommand<ReflectedResult>
{
    public Dictionary<string, ReflectedResult> Items { get; set; } = [];
    public Dictionary<int, NestedMapValue> Nested { get; set; } = [];
}

public class NestedMapValue
{
    public List<string> Tags { get; set; } = [];
    public ReflectedResult? Body { get; set; }
}

//DateTimeOffset has no inbuilt protobuf-net serializer and is denylisted - map values of it must still fail at Create
public class MapOfUnsupportedCommand : ICommand<ReflectedResult>
{
    public Dictionary<string, DateTimeOffset> When { get; set; } = [];
}

public class Outer
{
    //a nested type's FullName is "Ns.Outer+NestedCommand" and '+' is not a legal protobuf identifier
    public class NestedCommand : ICommand<ReflectedResult>
    {
        public string Name { get; set; } = "";
    }
}

public class ReflectedCommandHandler : ICommandHandler<ReflectedCommand, ReflectedResult>
{
    public Task<ReflectedResult> ExecuteAsync(ReflectedCommand cmd, CancellationToken ct)
        => Task.FromResult(new ReflectedResult { FullName = $"{cmd.FirstName} {cmd.LastName}" });
}

public class ReflectedEvent : IEvent
{
    public string Message { get; set; } = "";
}