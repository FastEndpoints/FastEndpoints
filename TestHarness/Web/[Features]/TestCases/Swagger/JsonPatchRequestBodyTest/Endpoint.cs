using Microsoft.AspNetCore.JsonPatch.SystemTextJson;

namespace TestCases.Swagger.JsonPatchRequestBodyTest;

sealed class Person
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}

sealed class Request
{
    [RouteParam]
    public int Id { get; set; }

    [FromBody]
    public JsonPatchDocument<Person> Patches { get; set; }
}

sealed class Endpoint : Endpoint<Request, Person>
{
    public override void Configure()
    {
        Patch("/json-patch-test/{id}");
        Description(b => b.Accepts<Request>("application/json-patch+json"));
        AllowAnonymous();
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        var person = new Person { FirstName = "first", LastName = "last" };

        req.Patches.ApplyTo(person, err => AddError(new(err.AffectedObject.GetType().Name, err.ErrorMessage)));

        ThrowIfAnyErrors();

        await Send.OkAsync(person, cancellation: ct);
    }
}
