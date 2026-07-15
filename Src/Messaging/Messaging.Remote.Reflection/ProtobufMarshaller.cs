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
    //the single source of truth for field numbers: reflection descriptors read their numbers back out of this same model.
    //note that only the *numbering* is shared - the CLR->proto type mapping in CommandDescriptorFactory is separate, so a
    //type it can't map is skipped there rather than mis-described.
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
        //protobuf has no top-level scalar, but protobuf-net already writes one (an event hub's subscriber id, an
        //ICommand<string> result) as field 1 of an implicit message - so no explicit wrapper is needed here. the descriptor
        //has to describe that same shape though, which CommandDescriptorFactory.BuildMessage does.
        //a collection payload (ICommand<List<Dto>>) is wrapped the same way, but the model still has to learn its element type.
        EnsureRegistered(ElementType(typeof(T)) ?? typeof(T));

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

        //nested/repeated member types need registering too, and must be added before anything serializes this type.
        //non-message leaves (BCL scalars, collections' elements that aren't messages) are only valid when protobuf-net
        //already has a serializer for them - otherwise we'd either write an empty sub-message (silent data loss) or
        //blow up later on first serialize. fail here so handler bind / client Register surfaces the gap at startup.
        foreach (var vm in Model[t].GetFields())
        {
            var member = MemberType(vm);

            if (IsMessage(member))
                Register(member);
            else if (!Model.CanSerialize(member))
            {
                throw new NotSupportedException(
                    $"Protobuf marshaller cannot serialize property type [{member.FullName}] on [{t.FullName}]. " +
                    "Use a type protobuf-net already supports (string, bool, integral/floating types, decimal, DateTime, " +
                    "DateOnly, TimeOnly, TimeSpan, Guid, byte[], Uri, enums, nested messages and collections of those), " +
                    "or annotate a supported surrogate with [ProtoContract]/[ProtoMember].");
            }
        }
    }

    //the type a member actually carries: the item type for a collection, unwrapped of Nullable<>
    internal static Type MemberType(ValueMember vm)
        => Unwrap(vm.ItemType ?? vm.MemberType);

    internal static Type Unwrap(Type t)
        => Nullable.GetUnderlyingType(t) ?? t;

    //mirrors the shape messagepack's contractless resolver produces: public read/write instance properties, alphabetical
    internal static IEnumerable<PropertyInfo> MessageProperties(Type t)
        => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p is { CanRead: true, CanWrite: true } && p.GetIndexParameters().Length == 0)
            .OrderBy(p => p.Name, StringComparer.Ordinal);

    internal static bool HasExplicitContract(Type t)
        => t.IsDefined(typeof(ProtoBuf.ProtoContractAttribute), false) || t.IsDefined(typeof(DataContractAttribute), false);

    //BCL / framework types that must never be registered as empty attribute-free messages. several of these (DateTimeOffset,
    //Version, Half, Int128, UInt128) have no public r/w properties, so Model.Add + zero fields used to serialize as 0A00 and
    //deserialize as default - silent data loss. others (DateOnly/TimeOnly/Uri/DateTime/...) have inbuilt protobuf-net
    //serializers and must stay non-messages so those serializers are used instead of a hollow contract.
    static readonly HashSet<Type> _nonMessageTypes =
    [
        typeof(string),
        typeof(decimal),
        typeof(DateTime),
        typeof(DateTimeOffset),
        typeof(TimeSpan),
        typeof(DateOnly),
        typeof(TimeOnly),
        typeof(Guid),
        typeof(byte[]),
        typeof(Uri),
        typeof(Version),
        typeof(Half),
        typeof(Int128),
        typeof(UInt128),
        typeof(object)
    ];

    //a "message" is a type protobuf can express as a top-level message. scalars, collections and nullable structs are not.
    //KeyValuePair is excluded on purpose: protobuf-net writes a dictionary as map entries, but its Key/Value are getter-only
    //so we'd describe it as an empty message. treating it as a non-message makes ProtoType throw, which skips the command
    //loudly rather than publishing a schema that says "list of empty objects".
    internal static bool IsMessage(Type t)
        => !(t.IsGenericType && t.GetGenericTypeDefinition() == typeof(KeyValuePair<,>)) &&
           Nullable.GetUnderlyingType(t) is null &&
           !t.IsPrimitive &&
           !t.IsEnum &&
           !_nonMessageTypes.Contains(t) &&
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
