using Grpc.Core;
using MessagePack;
using MessagePack.Resolvers;

namespace FastEndpoints;

internal sealed class MessagePackMarshaller<T> : Marshaller<T> where T : class
{
    private static readonly MessagePackSerializerOptions options = MessagePackSerializerOptions
        .Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithCompression(MessagePackCompression.Lz4Block);

    private static readonly Type t = typeof(T);

    public MessagePackMarshaller() : base(Serialize, Deserialize) { }

    public static T Deserialize(DeserializationContext ctx)
        => MessagePackSerializer.Deserialize<T>(ctx.PayloadAsReadOnlySequence(), options);

    public static void Serialize(T value, SerializationContext ctx)
    {
        MessagePackSerializer.Serialize(t, ctx.GetBufferWriter(), value, options);
        ctx.Complete();
    }
}