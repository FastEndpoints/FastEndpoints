using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Polymorphic types / inheritance in response DTOs in AOT mode
[JsonDerivedType(typeof(DogResponse), "dog")]
[JsonDerivedType(typeof(CatResponse), "cat")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
public abstract class AnimalResponse
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
}

public sealed class DogResponse : AnimalResponse
{
    public string Breed { get; set; } = string.Empty;
    public bool CanFetch { get; set; }
}

public sealed class CatResponse : AnimalResponse
{
    public bool IsIndoor { get; set; }
    public int LivesRemaining { get; set; }
}

public sealed class PolymorphicRequest
{
    [QueryParam]
    public string AnimalType { get; set; } = "dog";

    [QueryParam]
    public string Name { get; set; } = "Buddy";

    [QueryParam]
    public int Age { get; set; } = 3;
}

public sealed class PolymorphicEndpoint : Endpoint<PolymorphicRequest, AnimalResponse>
{
    public override void Configure()
    {
        Get("polymorphic-types");
        AllowAnonymous();
        SerializerContext<PolymorphicSerCtx>();
    }

    public override async Task HandleAsync(PolymorphicRequest req, CancellationToken ct)
    {
        AnimalResponse response = req.AnimalType.ToLowerInvariant() switch
        {
            "dog" => new DogResponse
            {
                Name = req.Name,
                Age = req.Age,
                Breed = "Labrador",
                CanFetch = true
            },
            "cat" => new CatResponse
            {
                Name = req.Name,
                Age = req.Age,
                IsIndoor = true,
                LivesRemaining = 9
            },
            _ => new DogResponse
            {
                Name = req.Name,
                Age = req.Age,
                Breed = "Unknown",
                CanFetch = false
            }
        };

        await Send.OkAsync(response, ct);
    }
}

[JsonSerializable(typeof(PolymorphicRequest))]
[JsonSerializable(typeof(AnimalResponse))]
[JsonSerializable(typeof(DogResponse))]
[JsonSerializable(typeof(CatResponse))]
public partial class PolymorphicSerCtx : JsonSerializerContext;
