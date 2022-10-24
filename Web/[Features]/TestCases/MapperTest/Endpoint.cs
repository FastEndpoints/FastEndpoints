namespace TestCases.MapperTest;

public class Endpoint : Endpoint<Request, Response, Mapper>
{
    private readonly ILogger _logger;

    public Endpoint(ILogger<Endpoint> logger)
    {
        _logger = logger;
    }

    public override void Configure() => Post("/test-cases/mapper-test");

    public override Task HandleAsync(Request r, CancellationToken t)
    {
        Response = Map.FromEntity(Map.ToEntity(r));

        _logger.LogInformation("Response sent...");

        return Task.CompletedTask;
    }
}

public class Mapper : Mapper<Request, Response, Person>
{
    public override Person ToEntity(Request r) => new()
    {
        Name = r.FirstName + " " + r.LastName,
        Age = r.Age
    };

    public override Response FromEntity(Person e) => new()
    {
        Name = e.Name,
        Age = e.Age
    };
}
