using Grpc.Core;

namespace FastEndpoints;

/// <summary>
/// creates the grpc <see cref="Marshaller{T}" /> used to (de)serialize commands, results and events on the wire.
/// register a custom implementation to change the wire format (e.g. protobuf for cross-ecosystem interop) via the
/// <c>marshaller</c> argument of <c>AddHandlerServer()</c> on the server, or via <c>RemoteConnection.MarshallerFactory</c> on the client.
/// defaults to messagepack.
/// </summary>
public interface IRpcMarshallerFactory
{
    /// <summary>
    /// creates a marshaller for the given command/result/event type.
    /// </summary>
    /// <typeparam name="T">the type being marshalled</typeparam>
    Marshaller<T> Create<T>() where T : class;

    /// <summary>
    /// the grpc method name that commands are bound under, for both the server and the client, so the two always agree.
    /// defaults to an empty name, which is what FastEndpoints has always bound (i.e. <c>/My.Command/</c>).
    /// <para>
    /// a protobuf wire format must override this with a real name: protobuf descriptors cannot represent an empty method
    /// name, so grpc reflection (and any protoc/buf codegen) would otherwise be unable to describe the handler.
    /// </para>
    /// </summary>
    string MethodName => "";
}

sealed class MessagePackMarshallerFactory : IRpcMarshallerFactory
{
    internal static readonly MessagePackMarshallerFactory Instance = new();

    public Marshaller<T> Create<T>() where T : class
        => new MessagePackMarshaller<T>();
}
