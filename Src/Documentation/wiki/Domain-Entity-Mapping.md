# domain entity mapping
for those of us who are not fans of AutoMapper and the like, this library offers a cleaner way to do manual mapping for request dto to domain entity and back from an entity to a response dto.

consider the following request, response and entity classes:

```csharp
public class Request
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string BirthDay { get; set; }
}

public class Response
{
    public int Id { get; set; }
    public string UserName { get; set; }
    public string FullName { get; set; }
    public int Age { get; set; }
}

public class Person
{
    public int Id { get; set; }
    public string FullName { get; set; }
    public DateOnly DateOfBirth { get; set; }
}
```

## mapping logic in the endpoint class
if you prefer to place your mapping logic in the endpoint definition itself, you can simply use the `EndpointWithMapping<TRequest,TResponse,TEntity>` generic overload to implement your endpoint and override the `MapToEntity()` and `MapFromEntity()` methods like so:

```csharp
public class SavePerson : EndpointWithMapping<Request, Response, Person>
{
    public override void Configure()
    {
        Put("/api/person");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        Person entity = MapToEntity(r);
        Response = MapFromEntity(entity);
        return SendAsync(Response);
    }

    public override Person MapToEntity(Request r) => new()
    {
        Id = r.Id,
        DateOfBirth = DateOnly.Parse(r.BirthDay),
        FullName = $"{r.FirstName} {r.LastName}"
    };

    public override Response MapFromEntity(Person e) => new()
    {
        Id = e.Id,
        FullName = e.FullName,
        UserName = $"USR{e.Id:0000000000}",
        Age = (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - e.DateOfBirth.DayNumber) / 365,
    };
}
```

## mapping logic in a separate class
if your preference is to keep the mapping logic in a class of it's own, you can create a separate mapper inheriting from `Mapper<TRequest, TResponse, TEntity>` like so:
```csharp
public class PersonMapper : Mapper<Request, Response, Person>
{
    public override Person ToEntity(Request r) => new()
    {
        Id = r.Id,
        DateOfBirth = DateOnly.Parse(r.BirthDay),
        FullName = $"{r.FirstName} {r.LastName}"
    };

    public override Response FromEntity(Person e) => new()
    {
        Id = e.Id,
        FullName = e.FullName,
        UserName = $"USR{e.Id:0000000000}",
        Age = (DateOnly.FromDateTime(DateTime.UtcNow).DayNumber - e.DateOfBirth.DayNumber) / 365,
    };
}
```
to use the above mapper you need to inherit your endpoint from `Endpoint<TRequest, TResponse, TMapper>` generic overload like so:
```csharp
public class SavePerson : Endpoint<Request, Response, PersonMapper>
{
    public override void Configure()
    {
        Put("/api/person");
        AllowAnonymous();
    }

    public override Task HandleAsync(Request r, CancellationToken c)
    {
        Person entity = Map.ToEntity(r);
        Response = Map.FromEntity(entity);
        return SendAsync(Response);
    }
}
```
the mapping logic can be accessed from the `Map` property of the endpoint class. that's all there's to it.

> [!NOTE]
> mapper classes are used as singletons for performance reasons. you should not maintain state in your mappers.