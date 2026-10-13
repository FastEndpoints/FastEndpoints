using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using FluentValidation.Internal;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace OpenApi;

[Collection(PolymorphicOneOfCollection.Name)]
public class ParameterAliasValidationTests
{
    [Theory]
    [InlineData("header", "name", "header", "name")]
    [InlineData("json-name", "name", "header", "name")]
    [InlineData("clr-name", "X-Token", "header", "headerToken")]
    [InlineData("query", "name", "query", "name")]
    public async Task parameter_constraints_follow_property_identity(string path, string parameterName, string location, string bodyName)
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
                o.Assemblies = [typeof(ParameterAliasValidationTests).Assembly];
                o.Filter = type => type == typeof(HeaderEndpoint) || type == typeof(JsonNameEndpoint) ||
                                   type == typeof(ClrNameEndpoint) || type == typeof(QueryEndpoint);
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
                var operation = document["paths"]![$"/parameter-alias/{path}"]!["post"]!;
                var parameter = operation["parameters"]!.AsArray().OfType<JsonNode>()
                                         .Single(p => p["name"]!.GetValue<string>() == parameterName && p["in"]!.GetValue<string>() == location);
                (parameter["required"]?.GetValue<bool>() ?? false).ShouldBeFalse();
                parameter["schema"]!["maxLength"]!.GetValue<int>().ShouldBe(20);
                (parameter["schema"]!["minLength"]?.GetValue<int>() ?? 0).ShouldBe(0);

                var body = operation["requestBody"]!["content"]!["application/json"]!["schema"]!;
                if (body["$ref"]?.GetValue<string>() is { } reference)
                    body = document["components"]!["schemas"]![reference.Split('/')[^1]]!;

                body["properties"]![bodyName]!["maxLength"]!.GetValue<int>().ShouldBe(3);
                body["required"]!.AsArray().Select(p => p!.GetValue<string>()).ShouldContain(bodyName);
            }

            await AssertResponse("abcdefghij", "ok", HttpStatusCode.OK);
            await AssertResponse(new string('a', 20), "ok", HttpStatusCode.OK);
            await AssertResponse(new string('a', 21), "ok", HttpStatusCode.BadRequest);
            await AssertResponse("abcdefghij", "long", HttpStatusCode.BadRequest);
            await AssertResponse("abcdefghij", "", HttpStatusCode.BadRequest);
            await AssertResponse(null, "ok", HttpStatusCode.OK);

            async Task AssertResponse(string? value, string bodyValue, HttpStatusCode expected)
            {
                var url = $"/parameter-alias/{path}";
                if (location == "query" && value is not null)
                    url += $"?{parameterName}={value}";

                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = new StringContent($"{{\"{bodyName}\":\"{bodyValue}\"}}", Encoding.UTF8, "application/json");
                if (location == "header" && value is not null)
                    request.Headers.TryAddWithoutValidation(parameterName, value);

                using var response = await client.SendAsync(request);
                response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
            }
        }
        finally
        {
            Config.EpOpts.RoutePrefix = previousRoutePrefix;
            Config.EpOpts.Filter = previousFilter;
            Config.EpOpts.Configurator = previousConfigurator;
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void rule_paths_recover_member_identity_and_preserve_explicit_overrides(bool usePropertyNamingPolicy)
    {
        using var globalState = new HostGlobalStateScope();
        Config.SerOpts.Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        ValidatorOptions.Global.PropertyNameResolver = (_, member, expression) =>
            JsonNamingPolicy.CamelCase.ConvertName(PropertyChain.FromExpression(expression).ToString());
        var validator = new InlineValidator<ClrNameRequest>();
        validator.RuleFor(x => x.HeaderToken).MaximumLength(20);
        validator.RuleFor(x => x.BodyValue).NotEmpty().MaximumLength(3);
        validator.RuleFor(x => x.HeaderToken).MinimumLength(2).OverridePropertyName(nameof(ClrNameRequest.BodyValue));

        var rules = validator.GetDictionaryOfRules(JsonNamingPolicy.CamelCase, usePropertyNamingPolicy, typeof(ClrNameRequest));

        rules["token_value"].Count.ShouldBe(1);
        rules["headerToken"].Count.ShouldBe(2);
        rules.Count.ShouldBe(2);
    }

    public class HeaderRequest
    {
        [FromHeader("name", isRequired: false)]
        public string? HeaderToken { get; set; }
        public string? Name { get; set; }
    }

    public class HeaderValidator : Validator<HeaderRequest>
    {
        public HeaderValidator()
        {
            RuleFor(x => x.HeaderToken).MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(3);
        }
    }

    public class HeaderEndpoint : Endpoint<HeaderRequest>
    {
        public override void Configure()
        {
            Post("/parameter-alias/header");
            AllowAnonymous();
            Validator<HeaderValidator>();
        }

        public override Task HandleAsync(HeaderRequest req, CancellationToken ct) => Send.OkAsync(ct);
    }

    public class JsonNameRequest
    {
        [FromHeader("name", isRequired: false), JsonPropertyName("token_value")]
        public string? HeaderToken { get; set; }
        [JsonPropertyName("name")]
        public string? BodyValue { get; set; }
    }

    public class JsonNameValidator : Validator<JsonNameRequest>
    {
        public JsonNameValidator()
        {
            RuleFor(x => x.HeaderToken).MaximumLength(20);
            RuleFor(x => x.BodyValue).NotEmpty().MaximumLength(3);
        }
    }

    public class JsonNameEndpoint : Endpoint<JsonNameRequest>
    {
        public override void Configure()
        {
            Post("/parameter-alias/json-name");
            AllowAnonymous();
            Validator<JsonNameValidator>();
        }

        public override Task HandleAsync(JsonNameRequest req, CancellationToken ct) => Send.OkAsync(ct);
    }

    public class ClrNameRequest
    {
        [FromHeader("X-Token", isRequired: false), JsonPropertyName("token_value")]
        public string? HeaderToken { get; set; }
        [JsonPropertyName("headerToken")]
        public string? BodyValue { get; set; }
    }

    public class ClrNameValidator : Validator<ClrNameRequest>
    {
        public ClrNameValidator()
        {
            RuleFor(x => x.HeaderToken).MaximumLength(20);
            RuleFor(x => x.BodyValue).NotEmpty().MaximumLength(3);
        }
    }

    public class ClrNameEndpoint : Endpoint<ClrNameRequest>
    {
        public override void Configure()
        {
            Post("/parameter-alias/clr-name");
            AllowAnonymous();
            Validator<ClrNameValidator>();
        }

        public override Task HandleAsync(ClrNameRequest req, CancellationToken ct) => Send.OkAsync(ct);
    }

    public class QueryRequest
    {
        [QueryParam, BindFrom("name")]
        public string? QueryToken { get; set; }
        [BindFrom("bodyName")]
        public string? Name { get; set; }
    }

    public class QueryValidator : Validator<QueryRequest>
    {
        public QueryValidator()
        {
            RuleFor(x => x.QueryToken).MaximumLength(20);
            RuleFor(x => x.Name).NotEmpty().MaximumLength(3);
        }
    }

    public class QueryEndpoint : Endpoint<QueryRequest>
    {
        public override void Configure()
        {
            Post("/parameter-alias/query");
            AllowAnonymous();
            Validator<QueryValidator>();
        }

        public override Task HandleAsync(QueryRequest req, CancellationToken ct) => Send.OkAsync(ct);
    }
}
