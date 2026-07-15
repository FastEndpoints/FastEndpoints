using System.Reflection;
using System.Runtime.Serialization;
using Grpc.Core;
using ProtoBuf.Meta;

namespace FastEndpoints;

/// <summary>
/// a protobuf wire-format marshaller factory for remote commands/results. supply it to <c>AddHandlerServer(marshaller: ...)</c>
/// on the server and to <see cref="RemoteConnectionCore.MarshallerFactory" /> on the client to put remote traffic on protobuf
/// instead of messagepack. this is what makes handlers describable by standard grpc reflection and by any protoc/buf toolchain.
/// <para>
/// command/result types need no attributes: their public read/write properties are mapped alphabetically and numbered from 1.
/// if a type does carry an explicit <c>[ProtoContract]</c>/<c>[DataContract]</c> however, that contract is honoured as authored.
/// </para>
/// <para>
/// IMPORTANT: the attribute-free numbering is positional, so adding, removing or renaming a property renumbers the fields after
/// it and breaks previously generated clients. annotate the type with <c>[ProtoContract]</c>/<c>[ProtoMember(n)]</c> to pin the
/// field numbers of any contract that has to survive changes.
/// </para>
/// </summary>
public sealed class ProtobufMarshallerFactory : IRpcMarshallerFactory
{
    //the single source of truth for field numbers - grpc reflection descriptors are generated from this same model,
    //so the bytes on the wire and the published schema cannot drift apart.
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
        //protobuf has no top-level scalar, so an event hub's subscriber id or an ICommand<string> result is wrapped in a message
        if (!IsMessage(typeof(T)))
            return new ScalarMarshaller<T>(Model);

        EnsureRegistered(typeof(T));

        return new ProtobufMarshaller<T>(Model);
    }

    //protobuf-net will not auto-add a type that carries no contract ("no contract can be inferred"), so attribute-free
    //command/result graphs have to be registered explicitly. the whole walk is done under the lock: it only runs while
    //handlers bind at startup, so contention is irrelevant and a half-registered graph is not worth the risk.
    internal void EnsureRegistered(Type t)
    {
        lock (_lock)
            Register(t);
    }

    void Register(Type t)
    {
        if (!IsMessage(t) || Model.IsDefined(t))
            return;

        if (HasExplicitContract(t))
        {
            Model.Add(t, applyDefaultBehaviour: true); //the user authored field numbers; let protobuf-net apply them as-is
        }
        else
        {
            var meta = Model.Add(t, applyDefaultBehaviour: false);
            var number = 1;

            foreach (var p in MessageProperties(t))
                meta.Add(number++, p.Name);
        }

        //nested/repeated member types need registering too, and must be added before anything serializes this type
        foreach (var vm in Model[t].GetFields())
            Register(vm.ItemType ?? vm.MemberType);
    }

    //mirrors the shape messagepack's contractless resolver produces: public read/write instance properties, alphabetical
    internal static IEnumerable<PropertyInfo> MessageProperties(Type t)
        => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true } && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    internal static bool HasExplicitContract(Type t)
        => t.IsDefined(typeof(ProtoBuf.ProtoContractAttribute), false) || t.IsDefined(typeof(DataContractAttribute), false);

    //a "message" is a type protobuf can express as a top-level message. scalars, collections and nullable structs are not.
    internal static bool IsMessage(Type t)
        => Nullable.GetUnderlyingType(t) is null &&
           !t.IsPrimitive &&
           !t.IsEnum &&
           t != typeof(string) &&
           t != typeof(decimal) &&
           t != typeof(DateTime) &&
           t != typeof(TimeSpan) &&
           t != typeof(Guid) &&
           t != typeof(byte[]) &&
           ElementType(t) is null &&
           (t.IsClass || t.IsValueType);

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

//carries a scalar (e.g. an event hub's subscriber id, or an ICommand<string> result) inside a one-field message, since
//protobuf has no top-level scalar. client and server both go through this, so the wire stays symmetrical.
sealed class ScalarMarshaller<T>(RuntimeTypeModel model) : Marshaller<T>(Serialize(model), Deserialize(model)) where T : class
{
    static Action<T, SerializationContext> Serialize(RuntimeTypeModel model)
        => (value, ctx) =>
           {
               model.Serialize(ctx.GetBufferWriter(), new Scalar<T> { Value = value });
               ctx.Complete();
           };

    static Func<DeserializationContext, T> Deserialize(RuntimeTypeModel model)
        => ctx => model.Deserialize<Scalar<T>>(ctx.PayloadAsReadOnlySequence()).Value!;
}

[ProtoBuf.ProtoContract(Name = "Scalar")] //explicit contract - the name is fixed so the generated descriptor doesn't leak a generic arity suffix
sealed class Scalar<T>
{
    [ProtoBuf.ProtoMember(1)]
    public T? Value { get; set; }
}
