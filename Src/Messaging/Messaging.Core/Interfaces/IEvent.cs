using System.Diagnostics.CodeAnalysis;

namespace FastEndpoints;

/// <summary>
/// marker interface for an event model
/// </summary>
// Open-generic subscriptions inspect the runtime event's interfaces, including on derived event types.
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
public interface IEvent;