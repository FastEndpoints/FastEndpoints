using System.Collections.Concurrent;

namespace FastEndpoints;

internal sealed class EventQueue<TEvent> : ConcurrentQueue<TEvent> where TEvent : class, IEvent
{
    internal DateTime LastDeQueueAt { private get; set; } = DateTime.UtcNow;
    private double ElapsedInactivityHrs => DateTime.UtcNow.Subtract(LastDeQueueAt).TotalHours;
    internal bool IsStale => (Count > 1000 && ElapsedInactivityHrs > 1) || (!IsEmpty && ElapsedInactivityHrs > 4);
}