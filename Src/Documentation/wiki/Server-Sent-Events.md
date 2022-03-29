# server-sent-events

[server-sent-events](https://developer.mozilla.org/en-US/docs/Web/API/Server-sent_events) can be used to push real-time data down to the web browser in an `async` manner without blocking threads using the `IAsyncIEnumerable` interface like so:

```csharp
public class EventStream : EndpointWithoutRequest
{
    public override void Configure()
    {
        Get("event-stream");
        AllowAnonymous();
        Options(x => x.RequireCors(p => p.AllowAnyOrigin()));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        //simply provide any IAsyncEnumerable<T> as argument
        await SendEventStream("my-event", GetDataStream(ct), ct);
    }

    private async IAsyncEnumerable<object> GetDataStream(
      [EnumeratorCancellation] CancellationToken cancellation)
    {
        while (!cancellation.IsCancellationRequested)
        {
            await Task.Delay(1000);
            yield return new { guid = Guid.NewGuid() };
        }
    }
}
```

in the browser, the event stream can be subscribed to and consumed using the `EventSource` object like so:
```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8" />
</head>
<body>
  <script>
    var sse = new EventSource('http://localhost:8080/event-stream');
    sse.addEventListener("my-event", e => console.log(e.data))
  </script>
</body>
</html>
```

if you are planning to create more than a handful of server-sent-event streams, it's a good idea to enable `http2` in kestrel and all upstream servers such as reverse proxies and CDNs so that data can be multiplexed between the web server and client using a low number of tcp connections. here's a [good read](https://ordina-jworks.github.io/event-driven/2021/04/23/SSE-with-HTTP2.html) on the subject. 