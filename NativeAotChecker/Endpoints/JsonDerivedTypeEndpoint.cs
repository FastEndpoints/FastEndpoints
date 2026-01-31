using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Base class with JsonDerivedType attributes for polymorphic serialization
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(DogAnimal), "dog")]
[JsonDerivedType(typeof(CatAnimal), "cat")]
[JsonDerivedType(typeof(BirdAnimal), "bird")]
public abstract class AnimalBase
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public abstract string Speak();
}

public class DogAnimal : AnimalBase
{
    public string Breed { get; set; } = string.Empty;
    public override string Speak() => "Woof!";
}

public class CatAnimal : AnimalBase
{
    public bool IsIndoor { get; set; }
    public override string Speak() => "Meow!";
}

public class BirdAnimal : AnimalBase
{
    public double Wingspan { get; set; }
    public override string Speak() => "Tweet!";
}

// Request with polymorphic property
public class JsonDerivedTypeRequest
{
    public AnimalBase? Animal { get; set; }
    public List<AnimalBase> Animals { get; set; } = [];
}

public class JsonDerivedTypeResponse
{
    public string AnimalName { get; set; } = string.Empty;
    public string AnimalType { get; set; } = string.Empty;
    public string AnimalSound { get; set; } = string.Empty;
    public int AnimalCount { get; set; }
    public List<string> AllSounds { get; set; } = [];
    public bool JsonDerivedTypeWorked { get; set; }
}

/// <summary>
/// Tests [JsonDerivedType] and [JsonPolymorphic] attributes in AOT mode.
/// AOT ISSUE: Polymorphic deserialization uses type discriminator mapping.
/// JsonDerivedType attribute discovery requires reflection.
/// Runtime type resolution for derived types uses Type.GetType.
/// </summary>
public class JsonDerivedTypeEndpoint : Endpoint<JsonDerivedTypeRequest, JsonDerivedTypeResponse>
{
    public override void Configure()
    {
        Post("json-derived-type-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(JsonDerivedTypeRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new JsonDerivedTypeResponse
        {
            AnimalName = req.Animal?.Name ?? "none",
            AnimalType = req.Animal?.GetType().Name ?? "none",
            AnimalSound = req.Animal?.Speak() ?? "silent",
            AnimalCount = req.Animals.Count,
            AllSounds = req.Animals.Select(a => a.Speak()).ToList(),
            JsonDerivedTypeWorked = req.Animal != null || req.Animals.Count > 0
        });
    }
}
