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
                o.Assemblies = [typeof(NullableCollectionValidationTests).Assembly];
                o.Filter = type => type == typeof(StrictEndpoint) || type == typeof(LooseEndpoint) ||
                                   type == typeof(FilteredEndpoint) || type == typeof(ConditionalEndpoint);
            });
            builder.Services.OpenApiDocument(o =>
            {
                o.DocumentName = "v1";
                o.ConfigureOpenApi = options => options.AddSchemaTransformer((schema, context, _) =>
                {
                    // ASP.NET Core does not infer nullable reference-type collection elements.
                    if ((context.JsonTypeInfo.Type == typeof(List<string>) || context.JsonTypeInfo.Type == typeof(string[])) &&
                        schema.Items is Microsoft.OpenApi.OpenApiSchema items)
                        items.Type |= Microsoft.OpenApi.JsonSchemaType.Null;

                    return Task.CompletedTask;
                });
            });

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

                var filtered = RequestSchema(document, "/nullable-collections/filtered");
                var filteredProperties = filtered["properties"]!;
                foreach (var name in new[] { "tags", "codes", "names", "scores" })
                {
                    var collection = Resolve(document, filteredProperties[name]!);
                    collection["type"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldContain("null");
                    var items = Resolve(document, collection["items"]!);
                    items["type"]!.ToJsonString().ShouldContain("\"null\"", customMessage: name);
                    (items["minLength"]?.GetValue<int>() ?? 0).ShouldBe(0);
                    items["pattern"].ShouldBeNull();
                    items["minimum"].ShouldBeNull();
                    items["maximum"].ShouldBeNull();
                    if (name == "tags")
                        items["maxLength"]!.GetValue<int>().ShouldBe(30);
                    else
                        items["maxLength"].ShouldBeNull();
                }
                var conditional = RequestSchema(document, "/nullable-collections/conditional");
                foreach (var name in new[] { "tags", "codes", "names", "scores" })
                {
                    var collection = Resolve(document, conditional["properties"]![name]!);
                    collection["type"]!.AsArray().Select(t => t!.GetValue<string>()).ShouldContain("null");
                    var items = Resolve(document, collection["items"]!);
                    items["type"]!.ToJsonString().ShouldContain("\"null\"", customMessage: name);
                    items["pattern"].ShouldBeNull();
                    (items["minLength"]?.GetValue<int>() ?? 0).ShouldBe(0);
                    items["minimum"].ShouldBeNull();
                    items["maximum"].ShouldBeNull();
                    if (name is "tags" or "codes")
                        items["maxLength"]!.GetValue<int>().ShouldBe(30);
                    else
                        items["maxLength"].ShouldBeNull();
                }
                conditional["required"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["requiredTags"]);
                Resolve(document, conditional["properties"]!["requiredTags"]!)["minItems"]!.GetValue<int>().ShouldBe(1);

                filtered["required"]!.AsArray().Select(n => n!.GetValue<string>()).ShouldBe(["requiredTags"]);
                Resolve(document, filteredProperties["requiredTags"]!)["minItems"]!.GetValue<int>().ShouldBe(1);
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

            await AssertResponse(client, "/nullable-collections/conditional", """{"tags":["INVALID"],"codes":["waytoolong"],"names":["x"],"scores":[-1],"requiredTags":["ok"]}""", HttpStatusCode.OK);
            await AssertResponse(client, "/nullable-collections/conditional", """{"enforce":true,"tags":["aaa"],"codes":["ok"],"names":["ok"],"scores":[3],"requiredTags":["ok"]}""", HttpStatusCode.OK);
            await AssertResponse(client, "/nullable-collections/conditional", """{"enforce":true,"tags":null,"codes":null,"names":null,"scores":null,"requiredTags":["ok"]}""", HttpStatusCode.OK);
            foreach (var invalid in new[]
                     {
                         """{"enforce":true,"tags":["INVALID"],"requiredTags":["ok"]}""",
                         """{"enforce":true,"codes":["waytoolong"],"requiredTags":["ok"]}""",
                         """{"enforce":true,"names":["x"],"requiredTags":["ok"]}""",
                         """{"enforce":true,"scores":[-1],"requiredTags":["ok"]}""",
                         """{"codes":["1234567890123456789012345678901"],"requiredTags":["ok"]}""",
                         """{"tags":["1234567890123456789012345678901"],"requiredTags":["ok"]}""",
                         """{"requiredTags":[]}"""
                     })
                await AssertResponse(client, "/nullable-collections/conditional", invalid, HttpStatusCode.BadRequest);

            await AssertResponse(client, "/nullable-collections/loose", """{"tags":["123456"],"names":[null]}""", HttpStatusCode.OK);
            await AssertResponse(client, "/nullable-collections/filtered", """{"tags":[null,"skip-long-value","ok"],"codes":[null,"skip"],"names":[null,""],"scores":[0,1,5],"requiredTags":["ok"]}""", HttpStatusCode.OK);
            await AssertResponse(client, "/nullable-collections/filtered", """{"tags":null,"codes":null,"names":null,"scores":null,"requiredTags":["ok"]}""", HttpStatusCode.OK);
            foreach (var invalid in new[]
                     {
                         """{"tags":["123456"],"requiredTags":["ok"]}""",
                         """{"tags":["skip-123456789012345678901234567890"],"requiredTags":["ok"]}""",
                         """{"codes":["INVALID"],"requiredTags":["ok"]}""",
                         """{"scores":[6],"requiredTags":["ok"]}""",
                         """{"requiredTags":[]}"""
                     })
                await AssertResponse(client, "/nullable-collections/filtered", invalid, HttpStatusCode.BadRequest);
        }
        finally
        {
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
        public bool Enforce { get; set; }
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

    public class FilteredValidator : Validator<Request>
    {
        public FilteredValidator()
        {
            RuleForEach(x => x.Tags).Where(x => x is not null && !x.StartsWith("skip")).NotNull().Length(2, 5);
            RuleForEach(x => x.Tags).MaximumLength(30);
            RuleForEach(x => x.Codes).WhereAsync(x => Task.FromResult(x is not null && x != "skip")).NotNull().Matches("^[a-z]+$");
            RuleForEach(x => x.Names).Where(x => !string.IsNullOrEmpty(x)).NotEmpty();
            RuleForEach(x => x.Scores).Where(x => x > 0).InclusiveBetween(1, 5);
            RuleFor(x => x.RequiredTags).NotEmpty();
        }
    }

    public class ConditionalValidator : Validator<Request>
    {
        public ConditionalValidator()
        {
            RuleForEach(x => x.Tags).MaximumLength(30).Matches("^a+$")
                                   .When(x => x.Enforce, ApplyConditionTo.CurrentValidator);
            When(x => x.Enforce, () => RuleForEach(x => x.Codes).MaximumLength(5));
            RuleForEach(x => x.Codes).MaximumLength(30);
            RuleForEach(x => x.Names).Length(2, 5).UnlessAsync((x, _) => Task.FromResult(!x.Enforce));
            WhenAsync((x, _) => Task.FromResult(x.Enforce), () => RuleForEach(x => x.Scores).InclusiveBetween(1, 5));
            RuleFor(x => x.RequiredTags).NotEmpty();
        }
    }

    public class ConditionalEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Post("/nullable-collections/conditional");
            AllowAnonymous();
            Validator<ConditionalValidator>();
        }

        public override Task HandleAsync(Request req, CancellationToken ct)
            => Send.OkAsync(ct);
    }

    public class FilteredEndpoint : Endpoint<Request>
    {
        public override void Configure()
        {
            Post("/nullable-collections/filtered");
            AllowAnonymous();
            Validator<FilteredValidator>();
        }

        public override Task HandleAsync(Request req, CancellationToken ct)
            => Send.OkAsync(ct);
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
