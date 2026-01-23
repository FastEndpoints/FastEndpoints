using System.Text.Json.Serialization;

namespace NativeAotChecker.Greeting;

public sealed class GreetingRequest
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

public sealed class GreetingResponse
{
    public string Message { get; set; }
}

public sealed class GreetingEndpoint : Endpoint<GreetingRequest, GreetingResponse>
{
    public override void Configure()
    {
        Post("hello");
        AllowAnonymous();
        SerializerContext<GreetingSerCtx>();
    }

    public override async Task HandleAsync(GreetingRequest r, CancellationToken c)
    {
        await Send.OkAsync(new() { Message = $"Hello {r.FirstName} {r.LastName}!" });
    }
}

[JsonSerializable(typeof(GreetingRequest)), JsonSerializable(typeof(GreetingResponse))]
public partial class GreetingSerCtx : JsonSerializerContext;