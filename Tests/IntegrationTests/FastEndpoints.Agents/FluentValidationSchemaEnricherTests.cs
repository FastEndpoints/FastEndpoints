using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;

namespace FastEndpoints.Agents.Tests;

public class FluentValidationSchemaEnricherTests
{
    class UserDto
    {
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public int Age { get; set; }
    }

    class UserValidator : AbstractValidator<UserDto>
    {
        public UserValidator()
        {
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(64).Matches("[A-Z]");
            RuleFor(x => x.Age).GreaterThanOrEqualTo(13).LessThanOrEqualTo(120);
        }
    }

    [Fact]
    public void Enrich_marks_required_properties()
    {
        var schema = JsonSchemaBuilder.Build(typeof(UserDto), JsonSerializerOptions.Default);
        FluentValidationSchemaEnricher.Enrich(schema, new UserValidator());

        var required = schema["required"]?.AsArray()
            .Select(n => n?.GetValue<string>())
            .ToHashSet() ?? [];

        required.ShouldContain("Email");
        required.ShouldContain("Password");
    }

    [Fact]
    public void Enrich_applies_length_and_pattern_on_strings()
    {
        var schema = JsonSchemaBuilder.Build(typeof(UserDto), JsonSerializerOptions.Default);
        FluentValidationSchemaEnricher.Enrich(schema, new UserValidator());

        var password = (JsonObject)schema["properties"]!["Password"]!;
        password["minLength"]!.GetValue<int>().ShouldBe(8);
        password["maxLength"]!.GetValue<int>().ShouldBe(64);
        password["pattern"]!.GetValue<string>().ShouldBe("[A-Z]");
    }

    [Fact]
    public void Enrich_applies_numeric_comparisons()
    {
        var schema = JsonSchemaBuilder.Build(typeof(UserDto), JsonSerializerOptions.Default);
        FluentValidationSchemaEnricher.Enrich(schema, new UserValidator());

        var age = (JsonObject)schema["properties"]!["Age"]!;
        age["minimum"]!.GetValue<int>().ShouldBe(13);
        age["maximum"]!.GetValue<int>().ShouldBe(120);
    }

    [Fact]
    public void Enrich_marks_email_format()
    {
        var schema = JsonSchemaBuilder.Build(typeof(UserDto), JsonSerializerOptions.Default);
        FluentValidationSchemaEnricher.Enrich(schema, new UserValidator());

        var email = (JsonObject)schema["properties"]!["Email"]!;
        email["format"]!.GetValue<string>().ShouldBe("email");
    }
}
