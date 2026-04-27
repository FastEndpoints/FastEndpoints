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

    static OpenApiPathItem CreatePathItem(HttpMethod method, string operationId)
        => new()
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation>
            {
                [method] = new() { OperationId = operationId }
            }
        };
}
