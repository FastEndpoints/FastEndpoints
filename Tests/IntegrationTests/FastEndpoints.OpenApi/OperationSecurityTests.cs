using FastEndpoints;
using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;

namespace OpenApi;

public class OperationSecurityTests
{
    [Theory]
    [InlineData(SecuritySchemeType.OAuth2)]
    [InlineData(SecuritySchemeType.OpenIdConnect)]
    [InlineData(SecuritySchemeType.Http)]
    [InlineData(SecuritySchemeType.ApiKey)]
    public void roles_do_not_become_security_scopes(SecuritySchemeType schemeType)
    {
        var options = new DocumentOptions();
        options.AddAuth("auth", new() { Type = schemeType }, ["api://example/access", "api://example/access"]);
        var document = CreateDocument();
        var context = new SharedContext();
        var metadata = new List<object> { new AuthorizeAttribute { Roles = "User,Admin" } };

        new OperationMetadataTransformer(options, context).ApplySecurityRequirements(
            document.Paths["/orders"]!.Operations![HttpMethod.Get], null, metadata, "GET:/orders", context.For(document));
        DocumentSecurityTransformer.Apply(document, options, context);

        var requirement = document.Paths["/orders"]!.Operations![HttpMethod.Get].Security.ShouldNotBeNull().ShouldHaveSingleItem();
        requirement.Single().Key.Reference.Id.ShouldBe("auth");
        requirement.Single().Value.ShouldBe(["api://example/access"]);
        ((AuthorizeAttribute)metadata[0]).Roles.ShouldBe("User,Admin");
    }

    [Fact]
    public void roles_without_configured_scopes_produce_an_empty_scope_list()
    {
        var options = new DocumentOptions();
        options.AddAuth("oauth2", new() { Type = SecuritySchemeType.OAuth2 });
        var document = CreateDocument();
        var context = new SharedContext();

        new OperationMetadataTransformer(options, context).ApplySecurityRequirements(
            document.Paths["/orders"]!.Operations![HttpMethod.Get], null,
            [new AuthorizeAttribute { Roles = "User" }], "GET:/orders", context.For(document));
        DocumentSecurityTransformer.Apply(document, options, context);

        document.Paths["/orders"]!.Operations![HttpMethod.Get].Security.ShouldNotBeNull()
                .ShouldHaveSingleItem().Single().Value.ShouldBeEmpty();
    }

    [Fact]
    public void endpoint_authentication_scheme_selection_is_preserved()
    {
        var options = new DocumentOptions();
        options.AddAuth("oauth2", new() { Type = SecuritySchemeType.OAuth2 }, ["orders.read"]);
        options.AddAuth("other", new() { Type = SecuritySchemeType.OAuth2 }, ["other.read"]);
        var document = CreateDocument();
        var context = new SharedContext();
        var definition = new EndpointDefinition(typeof(OrdersEndpoint), typeof(EmptyRequest), typeof(object));
        definition.AuthSchemes("oauth2");

        new OperationMetadataTransformer(options, context).ApplySecurityRequirements(
            document.Paths["/orders"]!.Operations![HttpMethod.Get], definition,
            [new AuthorizeAttribute { Roles = "User" }], "GET:/orders", context.For(document));
        DocumentSecurityTransformer.Apply(document, options, context);

        var requirement = document.Paths["/orders"]!.Operations![HttpMethod.Get].Security.ShouldNotBeNull().ShouldHaveSingleItem();
        requirement.Single().Key.Reference.Id.ShouldBe("oauth2");
        requirement.Single().Value.ShouldBe(["orders.read"]);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void anonymous_operations_have_no_security_requirements(bool hasAuthorization)
    {
        var options = new DocumentOptions();
        options.AddAuth("oauth2", new() { Type = SecuritySchemeType.OAuth2 }, ["orders.read"]);
        var document = CreateDocument();
        var context = new SharedContext();
        var operation = document.Paths["/orders"]!.Operations![HttpMethod.Get];
        operation.Security = [new()];
        var metadata = new List<object>();

        if (hasAuthorization)
        {
            metadata.Add(new AuthorizeAttribute { Roles = "User" });
            metadata.Add(new AllowAnonymousAttribute());
        }

        new OperationMetadataTransformer(options, context).ApplySecurityRequirements(
            operation, null, metadata, "GET:/orders", context.For(document));
        DocumentSecurityTransformer.Apply(document, options, context);

        operation.Security.ShouldBeEmpty();
        context.For(document).SecurityRequirements.ShouldBeEmpty();
    }

    static OpenApiDocument CreateDocument()
    {
        var document = new OpenApiDocument();
        document.Paths["/orders"] = new OpenApiPathItem
        {
            Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Get] = new() }
        };

        return document;
    }

    sealed class OrdersEndpoint : EndpointWithoutRequest
    {
        public override void Configure()
        {
            Get("/orders");
            Roles("User");
            AuthSchemes("oauth2");
        }
    }
}
