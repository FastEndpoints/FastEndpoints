namespace FastEndpoints
{
    /// <summary>
    /// use this base class to handle events published by the notification system
    /// </summary>
    /// <typeparam name="TEvent">the type of the event to handle</typeparam>
    public abstract class FastEventHandler<TEvent> : IEventHandler where TEvent : class, new()
    {
        public void Subscribe()
        {
            Event<TEvent>.OnReceived += (e, c) => HandleAsync(e, c);
        }

        /// <summary>
        /// this method will be called when an event of the specified type is published.
        /// </summary>
        /// <param name="eventModel">the event model/dto received</param>
        /// <param name="ct">an optional cancellation token</param>
        public abstract Task HandleAsync(TEvent eventModel, CancellationToken ct);
    }
}
