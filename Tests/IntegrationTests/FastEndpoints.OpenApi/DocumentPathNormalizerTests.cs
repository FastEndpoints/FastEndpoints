using System.Text.Json;
using FastEndpoints.OpenApi;
using Microsoft.OpenApi;

namespace OpenApi;

public class DocumentPathNormalizerTests
{
    [Fact]
    public void parameter_normalization_merges_paths_when_http_methods_differ()
    {
        var document = new OpenApiDocument();
        var getPath = CreatePathItem(HttpMethod.Get, "users_get");
        var postPath = CreatePathItem(HttpMethod.Post, "users_post");
        document.Paths["/users/{id:int}"] = getPath;
        document.Paths["/users/{id:guid}"] = postPath;

        DocumentPathNormalizer.NormalizeParameterNames(document);

        document.Paths.Count.ShouldBe(1);
        document.Paths.ContainsKey("/users/{id}").ShouldBeTrue();
        document.Paths["/users/{id}"]!.Operations!.ContainsKey(HttpMethod.Get).ShouldBeTrue();
        document.Paths["/users/{id}"]!.Operations!.ContainsKey(HttpMethod.Post).ShouldBeTrue();
    }

    [Fact]
    public void parameter_normalization_throws_clear_error_when_same_http_method_collides()
    {
        var document = new OpenApiDocument();
        var intPath = CreatePathItem(HttpMethod.Get, "users_get_int");
        var guidPath = CreatePathItem(HttpMethod.Get, "users_get_guid");
        document.Paths["/users/{id:int}"] = intPath;
        document.Paths["/users/{id:guid}"] = guidPath;

        var ex = Should.Throw<InvalidOperationException>(() => DocumentPathNormalizer.NormalizeParameterNames(document));

        ex.Message.ShouldContain("/users/{id}");
        ex.Message.ShouldContain("/users/{id:int}");
        ex.Message.ShouldContain("/users/{id:guid}");
        ex.Message.ShouldContain("Get");
    }

    [Fact]
    public void naming_policy_path_normalization_merges_paths_when_http_methods_differ()
    {
        var document = new OpenApiDocument();
        var upperPath = CreatePathItem(HttpMethod.Get, "items_get");
        var camelPath = CreatePathItem(HttpMethod.Post, "items_post");
        document.Paths["/items/{FirstName}"] = upperPath;
        document.Paths["/items/{firstName}"] = camelPath;

        var sharedCtx = new SharedContext { NamingPolicy = JsonNamingPolicy.CamelCase };
        var opts = new DocumentOptions { UsePropertyNamingPolicy = true };

        DocumentPathNormalizer.Apply(document, opts, sharedCtx);

        document.Paths.Count.ShouldBe(1);
        document.Paths.ContainsKey("/items/{firstName}").ShouldBeTrue();
        document.Paths["/items/{firstName}"]!.Operations!.ContainsKey(HttpMethod.Get).ShouldBeTrue();
        document.Paths["/items/{firstName}"]!.Operations!.ContainsKey(HttpMethod.Post).ShouldBeTrue();
    }

    [Fact]
    public void canonical_path_normalization_strips_constraints_and_catch_all_markers()
    {
        RouteTemplateHelpers.NormalizePath("~/files/{*slug:minlength(3)?}").ShouldBe("/files/{slug}");
        RouteTemplateHelpers.NormalizePath("/files/{**path=default}").ShouldBe("/files/{path}");
    }

    [Fact]
    public void normalized_catch_all_operation_keys_match_version_filter_and_security_lookup()
    {
        var document = new OpenApiDocument();
        document.Paths["/files/{*slug:int}"] = CreatePathItem(HttpMethod.Get, "catch_all");

        DocumentPathNormalizer.NormalizeParameterNames(document);

        var sharedCtx = new SharedContext();
        var normalizedPath = RouteTemplateHelpers.NormalizePath("/files/{*slug:int}");
        var operationKey = $"GET:{normalizedPath}";
        sharedCtx.Operations[operationKey] = new()
        {
            OperationKey = normalizedPath,
            DocumentPath = normalizedPath,
            HttpMethod = "GET",
            Version = 0,
            StartingReleaseVersion = 0,
            DeprecatedAt = 0,
            IsFastEndpoint = true
        };
        sharedCtx.SecurityRequirements[operationKey] = [("ApiKey", [])];
        var opts = new DocumentOptions();
        opts.AddAuth("ApiKey", new() { Type = SecuritySchemeType.ApiKey, Name = "api_key", In = ParameterLocation.Header });

        new DocumentVersionFilter(opts, sharedCtx).Apply(document);
        DocumentSecurityTransformer.Apply(document, opts, sharedCtx);

        var operation = document.Paths["/files/{slug}"]!.Operations![HttpMethod.Get];
        operation.Security.ShouldNotBeNull();
        operation.Security.Count.ShouldBe(1);
    }

    static OpenApiPathItem CreatePathItem(HttpMethod method, string operationId)
        => new()
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [method] = new() { OperationId = operationId }
            }
        };
}
