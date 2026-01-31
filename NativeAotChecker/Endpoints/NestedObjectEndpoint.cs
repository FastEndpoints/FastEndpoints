using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Deeply nested object binding from JSON in AOT mode
public sealed class Address
{
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public Country Country { get; set; } = new();
}

public sealed class Country
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public sealed class ContactInfo
{
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public Address Address { get; set; } = new();
}

public sealed class NestedObjectRequest
{
    public string Name { get; set; } = string.Empty;
    public ContactInfo Contact { get; set; } = new();
    public List<string> Tags { get; set; } = [];
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public sealed class NestedObjectResponse
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
    public int TagCount { get; set; }
    public int MetadataCount { get; set; }
    public string FullAddress { get; set; } = string.Empty;
}

public sealed class NestedObjectEndpoint : Endpoint<NestedObjectRequest, NestedObjectResponse>
{
    public override void Configure()
    {
        Post("nested-object");
        AllowAnonymous();
        SerializerContext<NestedObjectSerCtx>();
    }

    public override async Task HandleAsync(NestedObjectRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new NestedObjectResponse
        {
            Name = req.Name,
            Email = req.Contact.Email,
            Street = req.Contact.Address.Street,
            CountryCode = req.Contact.Address.Country.Code,
            TagCount = req.Tags.Count,
            MetadataCount = req.Metadata.Count,
            FullAddress = $"{req.Contact.Address.Street}, {req.Contact.Address.City}, {req.Contact.Address.ZipCode}, {req.Contact.Address.Country.Name}"
        }, ct);
    }
}

[JsonSerializable(typeof(NestedObjectRequest))]
[JsonSerializable(typeof(NestedObjectResponse))]
public partial class NestedObjectSerCtx : JsonSerializerContext;
