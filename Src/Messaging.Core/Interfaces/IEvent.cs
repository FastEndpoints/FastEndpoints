namespace FastEndpoints;

/// <summary>
/// marker interface for an event model
/// </summary>
public interface IEvent { }

/// <summary>
/// marker interface for a round robin event model.
/// a round robin event will only be delivered to just one of the subscribers connected to the hub in a round robin fashion.
/// if for example, there's two subscribers (A and B) connected, event 1 will be delivered to subscriber A.
/// event 2 will be delivered to subscriber B. event 3 will be delivered to subscriber A and so on.
/// </summary>
public interface IRoundRobinEvent : IEvent { }