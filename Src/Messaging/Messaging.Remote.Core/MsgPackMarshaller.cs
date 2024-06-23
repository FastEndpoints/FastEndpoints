using Grpc.Core;
using MessagePack;
using MessagePack.Resolvers;

namespace FastEndpoints;

sealed class MessagePackMarshaller<T>() : Marshaller<T>(Serialize, Deserialize) where T : class
{
    static readonly MessagePackSerializerOptions _options
        = MessagePackSerializerOptions
         .Standard
         .WithResolver(ContractlessStandardResolver.Instance)
         .WithCompression(MessagePackCompression.Lz4BlockArray);

    static readonly Type _t = typeof(T);

    public static T Deserialize(DeserializationContext ctx)
        => MessagePackSerializer.Deserialize<T>(ctx.PayloadAsReadOnlySequence(), _options);

    public static void Serialize(T value, SerializationContext ctx)
    {
        MessagePackSerializer.Serialize(_t, ctx.GetBufferWriter(), value, _options);
        ctx.Complete();
    }
}