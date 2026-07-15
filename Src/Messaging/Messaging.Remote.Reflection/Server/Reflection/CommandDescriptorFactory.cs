using FastEndpoints.Messaging.Remote.Reflection;
using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using ServiceDescriptor = Google.Protobuf.Reflection.ServiceDescriptor;

namespace FastEndpoints;

//builds Google.Protobuf descriptors for the bound command services straight from their CLR types - no attributes and
//no hand-authored .proto. field numbers are read back out of the protobuf marshaller's RuntimeTypeModel (the very model
//that does the serializing), so the published schema and the bytes on the wire cannot drift apart.
static class CommandDescriptorFactory
{
    internal static IReadOnlyList<ServiceDescriptor> Build(RpcSchemaRegistry registry, IRpcMarshallerFactory marshaller, ILogger logger)
    {
        if (marshaller is not ProtobufMarshallerFactory protobuf)
        {
            throw new InvalidOperationException(
                $"gRPC reflection needs a protobuf wire format. Supply a [{nameof(ProtobufMarshallerFactory)}] via AddHandlerServer(marshaller: ...). " +
                "The default messagepack format has no protobuf descriptors to publish.");
        }

        var descriptors = new List<ServiceDescriptor>();

        //each command gets its own self-contained file, built in isolation. a command that can't be described (an
        //unmapped property type, say) is skipped with a warning instead of taking every other handler's schema down with it.
        foreach (var svc in registry.Services.OrderBy(s => s.ServiceName, StringComparer.Ordinal))
        {
            try
            {
                descriptors.AddRange(FileDescriptor.BuildFromByteStrings([BuildFile(svc, protobuf).ToByteString()])[0].Services);
            }
            catch (NotSupportedException ex)
            {
                logger.CommandNotDescribable(svc.ServiceName, ex.Message); //a shape we knowingly don't map yet
            }
            catch (Exception ex)
            {
                //not an expected gap. still skipped so one command can't take the rest down, but logged as the bug it is.
                logger.CommandDescriptorFailure(ex, svc.ServiceName);
            }
        }

        return descriptors;
    }

    static FileDescriptorProto BuildFile(RpcServiceSchema svc, ProtobufMarshallerFactory protobuf)
    {
        //a nested type's FullName is "Ns.Outer+Inner", and '+' can't appear in a protobuf identifier. describing it as
        //"Ns.Inner" would publish a service name the server never bound, so skip it rather than lie about the route.
        if (svc.CommandType.IsNested)
            throw new NotSupportedException($"Nested command types can't be described: [{svc.ServiceName}] has no legal protobuf equivalent.");

        var package = svc.CommandType.Namespace ?? "";

        var fdp = new FileDescriptorProto
        {
            Name = $"{svc.ServiceName}.proto",
            Package = package,
            Syntax = "proto3"
        };

        //each file is self-contained. message names are prefixed with the command's simple name so that two services
        //sharing a type in the same namespace can't collide on a symbol, and so no message can shadow the service itself.
        var names = new Dictionary<Type, string>();
        CollectMessages(svc.CommandType, svc.CommandType.Name, names, protobuf);
        CollectMessages(svc.ResultType, svc.CommandType.Name, names, protobuf);

        foreach (var (t, name) in names)
            fdp.MessageType.Add(BuildMessage(t, name, package, names, protobuf));

        var service = new ServiceDescriptorProto { Name = svc.CommandType.Name }; //FQN = package.Name = the command's FullName = what FE binds

        service.Method.Add(
            new MethodDescriptorProto
            {
                Name = protobuf.MethodName, //the same name BaseHandlerExecutor.Bind binds the method under
                InputType = $".{package}.{names[svc.CommandType]}",   //leading dot + fully qualified
                OutputType = $".{package}.{names[svc.ResultType]}",
                ClientStreaming = svc.MethodType is MethodType.ClientStreaming or MethodType.DuplexStreaming,
                ServerStreaming = svc.MethodType is MethodType.ServerStreaming or MethodType.DuplexStreaming
            });

        fdp.Service.Add(service);

        return fdp;
    }

    static void CollectMessages(Type t, string prefix, Dictionary<Type, string> names, ProtobufMarshallerFactory protobuf)
    {
        if (!names.TryAdd(t, MessageName(prefix, t)))
            return;

        //a scalar command/result travels as field 1 of an implicit message, so there is no graph to walk
        if (!ProtobufMarshallerFactory.IsMessage(t))
            return;

        protobuf.EnsureRegistered(t);

        foreach (var member in protobuf.Model[t].GetFields().Select(ProtobufMarshallerFactory.MemberType).Where(ProtobufMarshallerFactory.IsMessage))
            CollectMessages(member, prefix, names, protobuf);
    }

    //messages are prefixed with the command's simple name so two services in one namespace can't collide on a shared type,
    //and so no message can shadow the service itself. the namespace is folded in because two types with the same simple name
    //in different namespaces would otherwise collide.
    static string MessageName(string prefix, Type t)
        => $"{prefix}__{Sanitize(TypeName(t))}";

    //deliberately not Type.FullName: for a generic that includes assembly-qualified arguments, so every message symbol would
    //be renamed by an assembly version bump and break already-generated clients.
    static string TypeName(Type t)
        => t.IsGenericType
               ? $"{t.Namespace}.{t.Name}.{string.Join('.', t.GetGenericArguments().Select(TypeName))}"
               : $"{t.Namespace}.{t.Name}";

    static string Sanitize(string name)
        => new([.. name.Select(c => char.IsLetterOrDigit(c) ? c : '_')]);

    static DescriptorProto BuildMessage(Type t, string name, string package, Dictionary<Type, string> names, ProtobufMarshallerFactory protobuf)
    {
        var msg = new DescriptorProto { Name = name };

        //protobuf has no top-level scalar, so protobuf-net writes a scalar/collection command or result as field 1 of an
        //implicit message. describe that same shape, or the payload can't be built from the published schema.
        if (!ProtobufMarshallerFactory.IsMessage(t))
        {
            msg.Field.Add(
                new FieldDescriptorProto
                {
                    Name = "Value",
                    Number = 1,
                    Label = FieldDescriptorProto.Types.Label.Optional,
                    Type = ProtoType(t)
                });

            return msg;
        }

        foreach (var vm in protobuf.Model[t].GetFields())
        {
            var repeated = vm.ItemType is not null;
            var member = ProtobufMarshallerFactory.MemberType(vm);

            var field = new FieldDescriptorProto
            {
                Name = vm.Name,
                Number = vm.FieldNumber,
                Label = repeated ? FieldDescriptorProto.Types.Label.Repeated : FieldDescriptorProto.Types.Label.Optional, //proto3 singular is LABEL_OPTIONAL
                Type = ProtoType(member)
            };

            if (field.Type == FieldDescriptorProto.Types.Type.Message)
                field.TypeName = $".{package}.{names[member]}";

            msg.Field.Add(field);
        }

        return msg;
    }

    static FieldDescriptorProto.Types.Type ProtoType(Type t)
    {
        t = Nullable.GetUnderlyingType(t) ?? t;

        if (t.IsEnum) //protobuf-net writes enums as varints, so int32 describes the wire accurately without an enum descriptor
            return FieldDescriptorProto.Types.Type.Int32;

        if (ProtobufMarshallerFactory.IsMessage(t))
            return FieldDescriptorProto.Types.Type.Message;

        return Type.GetTypeCode(t) switch
        {
            TypeCode.String => FieldDescriptorProto.Types.Type.String,
            TypeCode.Boolean => FieldDescriptorProto.Types.Type.Bool,
            TypeCode.SByte or TypeCode.Int16 or TypeCode.Int32 => FieldDescriptorProto.Types.Type.Int32,
            TypeCode.Int64 => FieldDescriptorProto.Types.Type.Int64,
            TypeCode.Byte or TypeCode.UInt16 or TypeCode.UInt32 => FieldDescriptorProto.Types.Type.Uint32,
            TypeCode.UInt64 => FieldDescriptorProto.Types.Type.Uint64,
            TypeCode.Double => FieldDescriptorProto.Types.Type.Double,
            TypeCode.Single => FieldDescriptorProto.Types.Type.Float,
            _ when t == typeof(byte[]) => FieldDescriptorProto.Types.Type.Bytes,

            //BCL types excluded from IsMessage (DateTime/DateTimeOffset/DateOnly/TimeOnly/TimeSpan/decimal/Guid/Uri/...) are
            //either protobuf-net bcl.* messages or other non-proto3 scalars. describing them faithfully is the next
            //increment - fail loudly rather than publish a schema that doesn't match the wire (or an empty nested message).
            _ => throw new NotSupportedException(
                     $"gRPC reflection cannot yet describe the property type [{t.Name}]. Supported: string, bool, integral/floating types, " +
                     "enums, byte[], nested message types and collections of those.")
        };
    }
}
