using Grpc.Core;
using MessagePack;
using MessagePack.Resolvers;

namespace FastEndpoints;

internal sealed class MsgPackMarshaller<T> : Marshaller<T> where T : class
{
    private static readonly MessagePackSerializerOptions options = MessagePackSerializerOptions
        .Standard
        .WithResolver(ContractlessStandardResolver.Instance)
        .WithCompression(MessagePackCompression.Lz4Block);

    private static readonly Type t = typeof(T);

    public MsgPackMarshaller() : base(Serialize, Deserialize) { }

    private static byte[] Serialize(T value)
        => MessagePackSerializer.Serialize(t, value, options);

    private static T Deserialize(byte[] bytes)
        => MessagePackSerializer.Deserialize<T>(bytes, options);
}
