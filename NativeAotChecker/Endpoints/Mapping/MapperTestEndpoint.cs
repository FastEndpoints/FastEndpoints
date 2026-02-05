namespace NativeAotChecker.Endpoints.Mapping;

public class MapperTestRequest
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class MapperTestResponse
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool MapperWasUsed { get; set; }
}

public class PersonEntity
{
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
}

public class MapperTestMapper : Mapper<MapperTestRequest, MapperTestResponse, PersonEntity>
{
    public override PersonEntity ToEntity(MapperTestRequest r)
        => new()
        {
            FullName = $"{r.FirstName} {r.LastName}",
            Age = r.Age
        };

    public override MapperTestResponse FromEntity(PersonEntity e)
        => new()
        {
            FullName = e.FullName,
            Age = e.Age,
            MapperWasUsed = true
        };
}

public sealed class MapperTestEndpoint : Endpoint<MapperTestRequest, MapperTestResponse, MapperTestMapper>
{
    public override void Configure()
    {
        Post("mapper-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(MapperTestRequest r, CancellationToken c)
    {
        var entity = Map.ToEntity(r);
        var response = Map.FromEntity(entity);
        await Send.OkAsync(response, c);
    }
}