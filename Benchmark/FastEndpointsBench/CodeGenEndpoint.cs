using FastEndpoints;
using FluentValidation;
using System.Text.Json.Serialization;

namespace FEBench;

[JsonSerializable(typeof(CodeGenRequest))]
[JsonSerializable(typeof(CodeGenResponse))]
public partial class SerializerCtx : JsonSerializerContext { }

public class CodeGenRequest
{
    public int Id { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public int Age { get; set; }
    public IEnumerable<string>? PhoneNumbers { get; set; }
}

public class CodeGenValidator : Validator<CodeGenRequest>
{
    public CodeGenValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().WithMessage("name needed");
        RuleFor(x => x.LastName).NotEmpty().WithMessage("last needed");
        RuleFor(x => x.Age).GreaterThan(10).WithMessage("too young");
        RuleFor(x => x.PhoneNumbers).NotEmpty().WithMessage("phone needed");
    }
}

public class CodeGenResponse
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public int Age { get; set; }
    public string? PhoneNumber { get; set; }
}

public class CodeGenEndpoint : Endpoint<CodeGenRequest, CodeGenResponse>
{
    public ILogger<Endpoint>? MyProperty { get; set; }

    //public CodeGenEndpoint(ILogger<Endpoint> logger)
    //{

    //}

    public override void Configure()
    {
        Verbs(Http.POST);
        Routes("/benchmark/codegen/{id}");
        AllowAnonymous();
        SerializerContext(SerializerCtx.Default);
    }

    public override Task HandleAsync(CodeGenRequest req, CancellationToken ct)
    {
        //Logger.LogInformation("request received!");

        //validator is automatically being run by FastEndpoints

        return SendAsync(new CodeGenResponse()
        {
            Id = req.Id,
            Name = req.FirstName + " " + req.LastName,
            Age = req.Age,
            PhoneNumber = req.PhoneNumbers?.FirstOrDefault()
        });
    }
}