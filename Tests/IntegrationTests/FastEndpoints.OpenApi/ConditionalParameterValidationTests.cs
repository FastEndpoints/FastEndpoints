using System.Net;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace OpenApi;

[Collection(PolymorphicOneOfCollection.Name)]
public class ConditionalParameterValidationTests
{
    [Fact]
    public async Task conditional_scalar_constraints_preserve_unconditional_components_and_runtime_behavior()
    {
        var previousRoutePrefix = Config.EpOpts.RoutePrefix;
        var previousFilter = Config.EpOpts.Filter;
        var previousConfigurator = Config.EpOpts.Configurator;

        try
        {
            using var globalState = new HostGlobalStateScope();
            Config.EpOpts.RoutePrefix = null;
            Config.EpOpts.Filter = null;
            Config.EpOpts.Configurator = null;
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddFastEndpoints(o =>
            {
                o.DisableAutoDiscovery = true;
                o.Assemblies = [typeof(ConditionalParameterValidationTests).Assembly];
                o.Filter = type => type == typeof(QueryEndpoint) || type == typeof(BodyEndpoint);
            });
            builder.Services.OpenApiDocument(o => o.DocumentName = "v1");

            await using var app = builder.Build();
            app.UseFastEndpoints();
            app.MapOpenApi();
            await app.StartAsync();
            using var client = app.GetTestClient();

            for (var generation = 0; generation < 2; generation++)
            {
                var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
                var parameters = document["paths"]!["/conditional-validation"]!["get"]!["parameters"]!.AsArray().OfType<JsonNode>();
                var querySchemas = parameters.ToDictionary(p => p["name"]!.GetValue<string>(), p => p["schema"]!);
                AssertSchemas(querySchemas);

                var body = document["paths"]!["/conditional-validation"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!;
                if (body["$ref"]?.GetValue<string>() is { } reference)
                    body = document["components"]!["schemas"]![reference.Split('/')[^1]]!;
                AssertSchemas(body["properties"]!.AsObject().ToDictionary(p => p.Key, p => p.Value!));
            }

            const string invalidValues = "name=x&pattern=1&comparison=0&inclusive=0&exclusive=0&email=x&mixed=x&current=x&shared=x&asyncName=x";
            using (var response = await client.GetAsync($"/conditional-validation?enabled=false&{invalidValues}"))
                response.StatusCode.ShouldBe(HttpStatusCode.OK);

            foreach (var value in new[] { "name=x", "pattern=1", "comparison=0", "inclusive=0", "exclusive=0", "email=x", "mixed=x", "current=x", "shared=x", "asyncName=x" })
            {
                using var response = await client.GetAsync($"/conditional-validation?enabled=true&{value}");
                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, value);
            }

            using (var response = await client.GetAsync("/conditional-validation?enabled=true&name=valid&pattern=abc&comparison=6&inclusive=6&exclusive=6&email=a@b&mixed=valid&current=valid&shared=valid&asyncName=valid"))
                response.StatusCode.ShouldBe(HttpStatusCode.OK);

            foreach (var value in new[] { "mixed=12345678901", "current=12345678901" })
            {
                using var response = await client.GetAsync($"/conditional-validation?enabled=false&{value}");
                response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, value);
            }
        }
        finally
        {
            Config.EpOpts.RoutePrefix = previousRoutePrefix;
            Config.EpOpts.Filter = previousFilter;
            Config.EpOpts.Configurator = previousConfigurator;
        }
    }

    static void AssertSchemas(Dictionary<string, JsonNode> schemas)
    {
        schemas["name"]["minLength"].ShouldBeNull();
        schemas["name"]["maxLength"].ShouldBeNull();
        schemas["pattern"]["pattern"].ShouldBeNull();
        schemas["comparison"]["exclusiveMinimum"].ShouldBeNull();
        schemas["comparison"]["exclusiveMaximum"].ShouldBeNull();
        schemas["inclusive"]["minimum"].ShouldBeNull();
        schemas["inclusive"]["maximum"].ShouldBeNull();
        schemas["exclusive"]["exclusiveMinimum"].ShouldBeNull();
        schemas["exclusive"]["exclusiveMaximum"].ShouldBeNull();
        schemas["email"]["format"].ShouldBeNull();
        schemas["email"]["pattern"].ShouldBeNull();
        schemas["shared"]["minLength"].ShouldBeNull();
        schemas["asyncName"]["minLength"].ShouldBeNull();
        foreach (var name in new[] { "mixed", "current" })
        {
            schemas[name]["minLength"]!.GetValue<int>().ShouldBe(0);
            schemas[name]["maxLength"]!.GetValue<int>().ShouldBe(10);
        }
    }

    public class Request
    {
        public bool Enabled { get; set; }
        public string? Name { get; set; }
        public string? Pattern { get; set; }
        public int Comparison { get; set; } = 6;
        public int Inclusive { get; set; } = 6;
        public int Exclusive { get; set; } = 6;
        public string? Email { get; set; }
        public string? Mixed { get; set; }
        public string? Current { get; set; }
        public string? Shared { get; set; }
        public string? AsyncName { get; set; }
    }

    public class RequestValidator : Validator<Request>
    {
        public RequestValidator()
        {
            RuleFor(x => x.Name).Length(5, 10).When(x => x.Enabled);
            RuleFor(x => x.Pattern).Matches("^[a-z]+$").When(x => x.Enabled);
            RuleFor(x => x.Comparison).GreaterThan(5).LessThan(10).When(x => x.Enabled);
            RuleFor(x => x.Inclusive).InclusiveBetween(5, 10).When(x => x.Enabled);
            RuleFor(x => x.Exclusive).ExclusiveBetween(5, 10).When(x => x.Enabled);
            RuleFor(x => x.Email).EmailAddress().When(x => x.Enabled);
            RuleFor(x => x.Mixed).MinimumLength(5).When(x => x.Enabled).MaximumLength(10);
            RuleFor(x => x.Current).MaximumLength(10).MinimumLength(5).When(x => x.Enabled, ApplyConditionTo.CurrentValidator);
            When(x => x.Enabled, () => RuleFor(x => x.Shared).MinimumLength(5));
            RuleFor(x => x.AsyncName).MinimumLength(5).WhenAsync((x, _) => Task.FromResult(x.Enabled));
        }
    }

    public class QueryEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Get("/conditional-validation");
            AllowAnonymous();
            Validator<RequestValidator>();
        }

        public override Task HandleAsync(Request req, CancellationToken ct)
            => Send.OkAsync(ct);
    }

    public class BodyEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Post("/conditional-validation");
            AllowAnonymous();
            Validator<RequestValidator>();
        }

        public override Task HandleAsync(Request req, CancellationToken ct)
            => Send.OkAsync(ct);
    }
}
