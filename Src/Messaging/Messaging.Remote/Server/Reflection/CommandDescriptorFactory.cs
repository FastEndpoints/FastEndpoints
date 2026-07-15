using Google.Protobuf;
using Google.Protobuf.Reflection;
using Grpc.Core;
using ServiceDescriptor = Google.Protobuf.Reflection.ServiceDescriptor;

namespace FastEndpoints;

//builds Google.Protobuf descriptors for the bound command services straight from their CLR types - no attributes and
//no hand-authored .proto. field numbers are read back out of the protobuf marshaller's RuntimeTypeModel (the very model
//that does the serializing), so the published schema and the bytes on the wire cannot drift apart.
static class CommandDescriptorFactory
{
    internal static IReadOnlyList<ServiceDescriptor> Build(RpcSchemaRegistry registry, IRpcMarshallerFactory marshaller)
    {
        if (marshaller is not ProtobufMarshallerFactory protobuf)
        {
            throw new InvalidOperationException(
                $"gRPC reflection needs a protobuf wire format. Supply a [{nameof(ProtobufMarshallerFactory)}] via AddHandlerServer(marshaller: ...). " +
                "The default messagepack format has no protobuf descriptors to publish.");
        }

        var files = registry.Services
                            .OrderBy(s => s.ServiceName, StringComparer.Ordinal)
                            .Select(s => BuildFile(s, protobuf).ToByteString())
                            .ToArray();

        return FileDescriptor.BuildFromByteStrings(files).SelectMany(f => f.Services).ToArray();
    }

    static FileDescriptorProto BuildFile(RpcServiceSchema svc, ProtobufMarshallerFactory protobuf)
    {
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
        if (!names.TryAdd(t, $"{prefix}__{t.Name}"))
            return;

        protobuf.EnsureRegistered(t);

        foreach (var member in protobuf.Model[t].GetFields().Select(f => f.ItemType ?? f.MemberType).Where(ProtobufMarshallerFactory.IsMessage))
            CollectMessages(member, prefix, names, protobuf);
    }

    static DescriptorProto BuildMessage(Type t, string name, string package, Dictionary<Type, string> names, ProtobufMarshallerFactory protobuf)
    {
        var msg = new DescriptorProto { Name = name };

        foreach (var vm in protobuf.Model[t].GetFields())
        {
            var repeated = vm.ItemType is not null;
            var member = vm.ItemType ?? vm.MemberType;

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

            //DateTime/TimeSpan/decimal/Guid are serialized by protobuf-net as its own bcl.* messages, which would need
            //those descriptors published alongside. describing them faithfully is the next increment - fail loudly
            //rather than publish a schema that doesn't match the wire.
            _ => throw new NotSupportedException(
                     $"gRPC reflection cannot yet describe the property type [{t.Name}]. Supported: string, bool, integral/floating types, " +
                     "enums, byte[], nested message types and collections of those.")
        };
    }
}
