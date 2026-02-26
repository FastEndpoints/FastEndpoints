using System.Runtime.CompilerServices;

namespace TestCases.EventStreamTest;

sealed record SomeNotification(string Name);

sealed record Request(string EventName, SomeNotification[] Notifications);

sealed class EventStreamEndpoint : Endpoint<Request>
{
    public override void Configure()
    {
        Post("test-cases/event-stream");
        AllowAnonymous();
        Summary(s =>
        {
            s.Summary = "event stream request endpoint summary";
        });
    }

    public override async Task HandleAsync(Request request, CancellationToken ct)
    {
        static async IAsyncEnumerable<SomeNotification> CreateEventStream(Request request, [EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var notification in request.Notifications)
            {
                yield return notification;
                await Task.Delay(100, ct); // Simulate some delay
            }
        }

        await Send.EventStreamAsync(request.EventName, CreateEventStream(request, ct), ct);
    }
}