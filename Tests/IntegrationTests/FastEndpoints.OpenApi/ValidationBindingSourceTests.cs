using System.Net;
using System.Security.Claims;
using System.Text;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace OpenApi;

[Collection(PolymorphicOneOfCollection.Name)]
public class ValidationBindingSourceTests
{
    [Fact]
    public async Task parameter_requiredness_matches_binding_source_and_runtime_validation()
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
                o.Assemblies = [typeof(ValidationBindingSourceTests).Assembly];
                o.Filter = type => type == typeof(HeaderEndpoint) || type == typeof(ClaimEndpoint) ||
                                   type == typeof(CookieEndpoint) || type == typeof(RequiredHeaderEndpoint) || type == typeof(PermissionEndpoint);
            });
            builder.Services.OpenApiDocument(o => o.DocumentName = "v1");

            await using var app = builder.Build();
            app.Use(async (context, next) =>
            {
                if (context.Request.Headers.TryGetValue("X-Test-Claim", out var value))
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", value == "empty-claim" ? string.Empty : value.ToString())]));

                if (context.Request.Headers.ContainsKey("X-Test-Permission"))
                    context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(Config.SecOpts.PermissionsClaimType, "edit")]));

                await next(context);
            });
            app.UseFastEndpoints();
            app.MapOpenApi();
            await app.StartAsync();
            using var client = app.GetTestClient();

            for (var generation = 0; generation < 2; generation++)
            {
                var document = JsonNode.Parse(await client.GetStringAsync("/openapi/v1.json"))!;
                AssertParameter(document, "header", "X-Name", "header", false);
                AssertParameter(document, "header", "X-Hidden-Name", "header", false);
                AssertParameter(document, "claim", "name", "query", false);
                AssertParameter(document, "cookie", "name", "cookie", true);
                AssertParameter(document, "permission", "allowed", "query", false, hasLengthConstraint: false);
                AssertParameter(document, "required-header", "X-Name", "header", true);

                var body = document["paths"]!["/binding-source/header"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!;
                if (body["$ref"]?.GetValue<string>() is { } reference)
                    body = document["components"]!["schemas"]![reference.Split('/')[^1]]!;

                body["properties"]!["name"]!["minLength"]!.GetValue<int>().ShouldBe(1);
                body["properties"]!["hiddenName"].ShouldBeNull();
            }

            await AssertResponse(client, "header", """{"name":"body","hiddenName":"body"}""", HttpStatusCode.OK);
            await AssertResponse(client, "header?name=query&hiddenName=query", "{}", HttpStatusCode.OK);
            await AssertResponse(client, "header", "{}", HttpStatusCode.BadRequest);
            await AssertResponse(client, "header", """{"name":"","hiddenName":""}""", HttpStatusCode.BadRequest);
            await AssertResponse(client, "header", "{}", HttpStatusCode.OK, ("X-Name", "source"), ("X-Hidden-Name", "source"));
            await AssertResponse(client, "header", """{"name":"body","hiddenName":"body"}""", HttpStatusCode.OK,
                                 ("X-Name", ""), ("X-Hidden-Name", ""));
            await AssertResponse(client, "header", """{"name":"body","hiddenName":"body"}""", HttpStatusCode.BadRequest,
                                 ("X-Name", " "), ("X-Hidden-Name", " "));

            await AssertResponse(client, "claim", """{"name":"body"}""", HttpStatusCode.OK);
            await AssertResponse(client, "claim?name=query", "{}", HttpStatusCode.OK);
            await AssertResponse(client, "claim", "{}", HttpStatusCode.BadRequest);
            await AssertResponse(client, "claim?name=", "{}", HttpStatusCode.BadRequest);
            await AssertResponse(client, "claim", "{}", HttpStatusCode.OK, ("X-Test-Claim", "source"));
            await AssertResponse(client, "claim?name=query", "{}", HttpStatusCode.BadRequest, ("X-Test-Claim", "empty-claim"));
            await AssertResponse(client, "claim?name=query", "{}", HttpStatusCode.BadRequest, ("X-Test-Claim", " "));
            await AssertResponse(client, "claim?name=", "{}", HttpStatusCode.OK, ("X-Test-Claim", "source"));

            await AssertResponse(client, "permission", """{"allowed":true}""", HttpStatusCode.OK);
            await AssertResponse(client, "permission?allowed=true", "{}", HttpStatusCode.BadRequest);
            await AssertResponse(client, "permission", "{}", HttpStatusCode.BadRequest);
            await AssertResponse(client, "permission?allowed=", "{}", HttpStatusCode.BadRequest);
            await AssertResponse(client, "permission", "{}", HttpStatusCode.OK, ("X-Test-Permission", "edit"));
            await AssertResponse(client, "permission?allowed=false", "{}", HttpStatusCode.OK, ("X-Test-Permission", "edit"));

            // Missing cookies overwrite earlier values, so NotEmpty makes this cookie indispensable.
            await AssertResponse(client, "cookie", """{"name":"body"}""", HttpStatusCode.BadRequest);
            await AssertResponse(client, "cookie?name=query", "{}", HttpStatusCode.BadRequest);
            await AssertResponse(client, "cookie", "{}", HttpStatusCode.BadRequest);
            await AssertResponse(client, "cookie", "{}", HttpStatusCode.OK, ("Cookie", "name=source"));
            await AssertResponse(client, "cookie", """{"name":"body"}""", HttpStatusCode.BadRequest, ("Cookie", "name="));

            await AssertResponse(client, "required-header", """{"name":"body"}""", HttpStatusCode.BadRequest);
            await AssertResponse(client, "required-header", "{}", HttpStatusCode.OK, ("X-Name", "source"));
            await AssertResponse(client, "required-header", "{}", HttpStatusCode.BadRequest, ("X-Name", ""));
        }
        finally
        {
            Config.EpOpts.RoutePrefix = previousRoutePrefix;
            Config.EpOpts.Filter = previousFilter;
            Config.EpOpts.Configurator = previousConfigurator;
        }
    }

    static void AssertParameter(JsonNode document, string path, string name, string location, bool required, bool hasLengthConstraint = true)
    {
        var parameter = document["paths"]![$"/binding-source/{path}"]!["post"]!["parameters"]!.AsArray().OfType<JsonNode>()
                                .Single(p => p["name"]!.GetValue<string>() == name && p["in"]!.GetValue<string>() == location);
        (parameter["required"]?.GetValue<bool>() ?? false).ShouldBe(required);
        if (hasLengthConstraint)
            parameter["schema"]!["minLength"]!.GetValue<int>().ShouldBe(1);
    }

    static async Task AssertResponse(HttpClient client, string path, string json, HttpStatusCode expected, params (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/binding-source/{path}");
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        foreach (var (name, value) in headers)
            request.Headers.TryAddWithoutValidation(name, value);

        using var response = await client.SendAsync(request);
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
    }

    public class HeaderRequest
    {
        public string? BodyValue { get; set; }

        [FromHeader("X-Name", isRequired: false)]
        public string? Name { get; set; }

        [FromHeader("X-Hidden-Name", isRequired: false, removeFromSchema: true)]
        public string? HiddenName { get; set; }
    }

    public class HeaderValidator : Validator<HeaderRequest>
    {
        public HeaderValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
            RuleFor(x => x.HiddenName).NotEmpty();
        }
    }

    public class HeaderEndpoint : Endpoint<HeaderRequest>
    {
        public override void Configure()
        {
            Post("/binding-source/header");
            AllowAnonymous();
            Validator<HeaderValidator>();
        }

        public override Task HandleAsync(HeaderRequest req, CancellationToken ct)
            => Send.OkAsync(ct);
    }

    public class ClaimRequest
    {
        [FromClaim("name", isRequired: false), QueryParam]
        public string? Name { get; set; }
    }

    public class ClaimValidator : Validator<ClaimRequest>
    {
        public ClaimValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    public class ClaimEndpoint : Endpoint<ClaimRequest>
    {
        public override void Configure()
        {
            Post("/binding-source/claim");
            AllowAnonymous();
            Validator<ClaimValidator>();
        }

        public override Task HandleAsync(ClaimRequest req, CancellationToken ct)
            => Send.OkAsync(ct);
    }

    public class PermissionRequest
    {
        [HasPermission("edit", isRequired: false), QueryParam]
        public bool Allowed { get; set; }
    }

    public class PermissionValidator : Validator<PermissionRequest>
    {
        public PermissionValidator() => RuleFor(x => x.Allowed).NotEmpty();
    }

    public class PermissionEndpoint : Endpoint<PermissionRequest>
    {
        public override void Configure()
        {
            Post("/binding-source/permission");
            AllowAnonymous();
            Validator<PermissionValidator>();
        }

        public override Task HandleAsync(PermissionRequest req, CancellationToken ct)
            => Send.OkAsync(ct);
    }

    public class CookieRequest
    {
        [FromCookie("name", isRequired: false)]
        public string? Name { get; set; }
    }

    public class CookieValidator : Validator<CookieRequest>
    {
        public CookieValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    public class CookieEndpoint : Endpoint<CookieRequest>
    {
        public override void Configure()
        {
            Post("/binding-source/cookie");
            AllowAnonymous();
            Validator<CookieValidator>();
        }

        public override Task HandleAsync(CookieRequest req, CancellationToken ct)
            => Send.OkAsync(ct);
    }

    public class RequiredHeaderRequest
    {
        [FromHeader("X-Name")]
        public string? Name { get; set; }
    }

    public class RequiredHeaderValidator : Validator<RequiredHeaderRequest>
    {
        public RequiredHeaderValidator() => RuleFor(x => x.Name).NotEmpty();
    }

    public class RequiredHeaderEndpoint : Endpoint<RequiredHeaderRequest>
    {
        public override void Configure()
        {
            Post("/binding-source/required-header");
            AllowAnonymous();
            Validator<RequiredHeaderValidator>();
        }

        public override Task HandleAsync(RequiredHeaderRequest req, CancellationToken ct)
            => Send.OkAsync(ct);
    }
}
