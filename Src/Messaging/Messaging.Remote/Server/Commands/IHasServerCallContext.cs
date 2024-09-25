using Grpc.Core;

namespace FastEndpoints;

/// <summary>
/// implement this interface on command handler classes in order to access the <see cref="ServerCallContext" />
/// </summary>
public interface IHasServerCallContext
{
    public ServerCallContext ServerCallContext { get; set; }
}