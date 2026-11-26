using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints.Binding;

// covers source generated reflection data matching runtime reflection for overridden, hidden and [JsonIgnore] properties
public abstract class ReflectionParityBase
{
    public virtual string Name { get; set; } = null!;

    [JsonIgnore]
    public virtual string? Secret { get; set; }
}

public class ReflectionParityRequest : ReflectionParityBase
{
    public override string Name { get; set; } = null!;

    // inherits [JsonIgnore] from the base property, so it must not be bound
    public override string? Secret { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Note { get; set; }

    [FromQuery]
    public ReflectionParityAddress Address { get; set; } = null!;
}

public class ReflectionParityAddressBase
{
    public string Id { get; set; } = null!;
}

// hides the base property with a different type, so the generated lookup must not be ambiguous
public class ReflectionParityAddress : ReflectionParityAddressBase
{
    public new int Id { get; set; }
}

public class ReflectionParityResponse
{
    public string Name { get; set; } = null!;
    public string? Note { get; set; }
    public string? Secret { get; set; }
    public int Id { get; set; }
}

public class ReflectionParityEndpoint : Endpoint<ReflectionParityRequest, ReflectionParityResponse>
{
    public override void Configure()
    {
        Get("reflection-parity/{name}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ReflectionParityRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Name = req.Name,
                Note = req.Note,
                Secret = req.Secret,
                Id = req.Address.Id
            },
            ct);
    }
}
