using System.Net;
using System.Text;
using FastEndpoints;
using FastEndpoints.OpenApi;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;

namespace OpenApi;

[Collection(PolymorphicOneOfCollection.Name)]
public class NullableCollectionValidationTests
{
    [Fact]
    public async Task nullable_collection_item_rules_match_runtime_validation_and_stay_operation_local()
    {
        var previousResolver = MainExtensions.HostServiceResolver;
        var previousSerializerConfigured = MainExtensions.SerializerConfigured;
        var previousSerializerOptions = Config.SerOpts.Options;
        var previousNameResolver = ValidatorOptions.Global.PropertyNameResolver;
        var previousRoutePrefix = Config.EpOpts.RoutePrefix;
        var previousFilter = Config.EpOpts.Filter;
        var previousConfigurator = Config.EpOpts.Configurator;

        try
        {
            Config.EpOpts.RoutePrefix = null;
            Config.EpOpts.Filter = null;
            Config.EpOpts.Configurator = null;
            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddFastEndpoints(o =>
            {
                o.DisableAutoDiscovery = true;
                o.Assemblies = [typeof(NullableCollectionValidationTests).Assembly];
                o.Filter = type => type == typeof(StrictEndpoint) || type == typeof(LooseEndpoint);
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
                var strict = RequestSchema(document, "/nullable-collections/strict");
                var loose = RequestSchema(document, "/nullable-collections/loose");
                var properties = strict["properties"]!;
                var looseProperties = loose["properties"]!;
                var tags = Resolve(document, properties["tags"]!);
                var codes = Resolve(document, properties["codes"]!);
                var scores = Resolve(document, properties["scores"]!);
                var names = Resolve(document, properties["names"]!);

                foreach (var collection in new[] { tags, codes, scores, names })
                    collection["type"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldContain("null");

                var tagItems = Resolve(document, tags["items"]!);
                (tagItems["maxLength"]?.GetValue<int>()).ShouldBe(5);
                tagItems["type"]!.GetValue<string>().ShouldBe("string");
                Resolve(document, codes["items"]!)["maxLength"]!.GetValue<int>().ShouldBe(20);
                var scoreItems = Resolve(document, scores["items"]!);
                scoreItems["minimum"]!.GetValue<int>().ShouldBe(1);
                scoreItems["maximum"]!.GetValue<int>().ShouldBe(5);
                scoreItems["type"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldContain("null");
                var nameItems = Resolve(document, names["items"]!);
                nameItems["minLength"]!.GetValue<int>().ShouldBe(1);
                nameItems["type"]!.GetValue<string>().ShouldBe("string");

                strict["required"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["requiredTags"]);
                var requiredTags = Resolve(document, properties["requiredTags"]!);
                requiredTags["minItems"]!.GetValue<int>().ShouldBe(1);
                Resolve(document, requiredTags["items"]!)["maxLength"]!.GetValue<int>().ShouldBe(7);
                Resolve(document, Resolve(document, properties["untouched"]!)["items"]!)["maxLength"].ShouldBeNull();

                Resolve(document, Resolve(document, looseProperties["tags"]!)["items"]!)["maxLength"]!.GetValue<int>().ShouldBe(30);
                Resolve(document, Resolve(document, looseProperties["codes"]!)["items"]!)["maxLength"].ShouldBeNull();
                Resolve(document, Resolve(document, looseProperties["names"]!)["items"]!)["minLength"].ShouldBeNull();
                (loose["required"]?.AsArray().Count ?? 0).ShouldBe(0);
            }

            await AssertResponse(client, "/nullable-collections/strict", """{"requiredTags":["ok"]}""", HttpStatusCode.OK);
            await AssertResponse(client, "/nullable-collections/strict", """{"tags":null,"codes":null,"scores":null,"names":null,"requiredTags":["ok"]}""", HttpStatusCode.OK);
            await AssertResponse(client, "/nullable-collections/strict", """{"tags":["short"],"codes":["code"],"scores":[null,1,5],"names":["name"],"requiredTags":["ok"]}""", HttpStatusCode.OK);

            foreach (var invalid in new[]
                     {
                         """{"tags":["123456"],"requiredTags":["ok"]}""",
                         """{"tags":[null],"requiredTags":["ok"]}""",
                         """{"codes":["123456789012345678901"],"requiredTags":["ok"]}""",
                         """{"scores":[6],"requiredTags":["ok"]}""",
                         """{"names":[null],"requiredTags":["ok"]}""",
                         """{"names":[""],"requiredTags":["ok"]}""",
                         """{"requiredTags":[]}"""
                     })
                await AssertResponse(client, "/nullable-collections/strict", invalid, HttpStatusCode.BadRequest);

            await AssertResponse(client, "/nullable-collections/loose", """{"tags":["123456"],"names":[null]}""", HttpStatusCode.OK);
        }
        finally
        {
            Config.SerOpts.Options = previousSerializerOptions;
            MainExtensions.SerializerConfigured = previousSerializerConfigured;
            ValidatorOptions.Global.PropertyNameResolver = previousNameResolver;
            MainExtensions.HostServiceResolver = previousResolver;
            Config.EpOpts.RoutePrefix = previousRoutePrefix;
            Config.EpOpts.Filter = previousFilter;
            Config.EpOpts.Configurator = previousConfigurator;
        }
    }

    static JsonNode RequestSchema(JsonNode document, string path)
        => Resolve(document, document["paths"]![path]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!);

    static JsonNode Resolve(JsonNode document, JsonNode schema)
    {
        while (schema["$ref"]?.GetValue<string>() is { } reference)
            schema = document["components"]!["schemas"]![reference.Split('/')[^1]]!;

        return schema;
    }

    static async Task AssertResponse(HttpClient client, string path, string json, HttpStatusCode expected)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await client.PostAsync(path, content);
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync());
    }

    public class Request
    {
        public List<string?>? Tags { get; set; }
        public string?[]? Codes { get; set; }
        public int?[]? Scores { get; set; }
        public List<string?>? Names { get; set; }
        public List<string?> RequiredTags { get; set; } = [];
        public List<string?>? Untouched { get; set; }
    }

    public class StrictValidator : Validator<Request>
    {
        public StrictValidator()
        {
            RuleForEach(x => x.Tags).NotNull().MaximumLength(5);
            RuleForEach(x => x.Codes).MaximumLength(20);
            RuleForEach(x => x.Scores).InclusiveBetween(1, 5);
            RuleForEach(x => x.Names).NotEmpty();
            RuleFor(x => x.RequiredTags).NotEmpty();
            RuleForEach(x => x.RequiredTags).MaximumLength(7);
        }
    }

    public class LooseValidator : Validator<Request>
    {
        public LooseValidator()
        {
            RuleForEach(x => x.Tags).MaximumLength(30);
            RuleForEach(x => x.Names).NotNull().When(_ => false);
        }
    }

    public class StrictEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Post("/nullable-collections/strict");
            AllowAnonymous();
            Validator<StrictValidator>();
        }

        public override Task HandleAsync(Request req, CancellationToken ct)
            => Send.OkAsync(ct);
    }

    public class LooseEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Post("/nullable-collections/loose");
            AllowAnonymous();
            Validator<LooseValidator>();
        }

        public override Task HandleAsync(Request req, CancellationToken ct)
            => Send.OkAsync(ct);
    }
}
