using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Mapper in AOT mode
public sealed class MapperRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int BirthYear { get; set; }
}

public sealed class MapperResponse
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public string EntityId { get; set; } = string.Empty;
}

// Entity class for mapping
public sealed class PersonEntity
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

// Mapper implementation
public sealed class PersonMapper : Mapper<MapperRequest, MapperResponse, PersonEntity>
{
    public override PersonEntity ToEntity(MapperRequest r)
    {
        return new PersonEntity
        {
            FullName = $"{r.FirstName} {r.LastName}",
            Age = DateTime.Now.Year - r.BirthYear
        };
    }

    public override MapperResponse FromEntity(PersonEntity e)
    {
        return new MapperResponse
        {
            FullName = e.FullName,
            Age = e.Age,
            EntityId = e.Id
        };
    }
}

public sealed class MapperEndpoint : Endpoint<MapperRequest, MapperResponse, PersonMapper>
{
    public override void Configure()
    {
        Post("mapper-test");
        AllowAnonymous();
        SerializerContext<MapperSerCtx>();
    }

    public override async Task HandleAsync(MapperRequest req, CancellationToken ct)
    {
        // Use mapper to convert request -> entity -> response
        var entity = Map.ToEntity(req);
        var response = Map.FromEntity(entity);
        
        await Send.OkAsync(response, ct);
    }
}

[JsonSerializable(typeof(MapperRequest))]
[JsonSerializable(typeof(MapperResponse))]
public partial class MapperSerCtx : JsonSerializerContext;
