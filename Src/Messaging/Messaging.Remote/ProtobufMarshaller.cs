using System.Reflection;
using Grpc.Core;
using ProtoBuf.Meta;

namespace FastEndpoints;

/// <summary>
/// a protobuf wire-format marshaller factory for remote commands/results. supply it to
/// <see cref="HandlerServerExtensions.AddHandlerServer(Microsoft.Extensions.DependencyInjection.IServiceCollection,Action{Grpc.AspNetCore.Server.GrpcServiceOptions}?,IRpcMarshallerFactory?)" />
/// on the server and to <see cref="RemoteConnectionCore.MarshallerFactory" /> on the client to put remote traffic on protobuf
/// instead of messagepack. this is what makes the commands describable by standard grpc reflection and by any protoc/buf toolchain.
/// <para>
/// command/result types need no attributes: public instance properties are mapped alphabetically, numbered from 1, mirroring the
/// contractless shape of the default messagepack wire format.
/// </para>
/// </summary>
public sealed class ProtobufMarshallerFactory : IRpcMarshallerFactory
{
    //this model is the single source of truth for field numbers. the grpc reflection descriptors are generated from it
    //(see CommandDescriptorFactory), so the bytes on the wire and the published schema cannot drift apart.
    internal RuntimeTypeModel Model { get; } = RuntimeTypeModel.Create();

    readonly object _lock = new();

    /// <summary>
    /// protobuf descriptors cannot represent an empty method name, so protobuf-mode commands are bound under a real one.
    /// this keeps the published schema identical to what is actually served, so grpcurl can both describe and invoke a handler.
    /// </summary>
    public string MethodName => "Execute";

    /// <inheritdoc />
    public Marshaller<T> Create<T>() where T : class
    {
        EnsureRegistered(typeof(T));

        return new ProtobufMarshaller<T>(Model);
    }

    //registers a type (and everything reachable from it) contractlessly. idempotent and safe to call concurrently.
    internal void EnsureRegistered(Type t)
    {
        if (Model.IsDefined(t))
            return;

        if (!IsMessage(t))
        {
            throw new NotSupportedException(
                $"The protobuf wire format needs a message type, but [{t.Name}] is a scalar. Wrap it in a class with a single property. " +
                "This affects commands with primitive results (e.g. ICommand<string>) and event hubs.");
        }

        lock (_lock)
        {
            if (!Model.IsDefined(t))
            {
                var meta = Model.Add(t, applyDefaultBehaviour: false);
                var number = 1;

                foreach (var p in MessageProperties(t))
                    meta.Add(number++, p.Name);
            }
        }

        foreach (var p in MessageProperties(t))
        {
            var member = ElementType(p.PropertyType) ?? p.PropertyType;

            if (IsMessage(member))
                EnsureRegistered(member);
        }
    }

    //mirrors messagepack's contractless resolver: public instance properties, alphabetical, numbered from 1
    internal static IEnumerable<PropertyInfo> MessageProperties(Type t)
        => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true } && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    //a "message" is anything that isn't a protobuf scalar. strings/primitives/collections are not messages themselves.
    internal static bool IsMessage(Type t)
        => !t.IsPrimitive &&
           !t.IsEnum &&
           t != typeof(string) &&
           t != typeof(decimal) &&
           t != typeof(DateTime) &&
           t != typeof(TimeSpan) &&
           t != typeof(Guid) &&
           t != typeof(byte[]) &&
           ElementType(t) is null &&
           (t.IsClass || (t.IsValueType && Nullable.GetUnderlyingType(t) is null));

    //the item type of a repeated (collection) member, or null when the member isn't a collection
    internal static Type? ElementType(Type t)
    {
        if (t == typeof(string) || t == typeof(byte[]))
            return null;

        if (t.IsArray)
            return t.GetElementType();

        return t.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                ?.GetGenericArguments()[0];
    }
}

//protobuf counterpart of MessagePackMarshaller - same role, protobuf on the wire instead of messagepack.
sealed class ProtobufMarshaller<T>(RuntimeTypeModel model) : Marshaller<T>(Serialize(model), Deserialize(model)) where T : class
{
    static Action<T, SerializationContext> Serialize(RuntimeTypeModel model)
        => (value, ctx) =>
           {
               model.Serialize(ctx.GetBufferWriter(), value);
               ctx.Complete();
           };

    static Func<DeserializationContext, T> Deserialize(RuntimeTypeModel model)
        => ctx => model.Deserialize<T>(ctx.PayloadAsReadOnlySequence());
}
