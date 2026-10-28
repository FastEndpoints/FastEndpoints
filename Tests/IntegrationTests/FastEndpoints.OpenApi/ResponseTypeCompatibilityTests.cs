using FastEndpoints.OpenApi;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace OpenApi;

public class ResponseTypeCompatibilityTests
{
    [Fact]
    public void last_declared_type_wins_regardless_of_explorer_sort_order()
    {
        var description = new ApiDescription { ActionDescriptor = new() };
        description.ActionDescriptor.EndpointMetadata =
        [
            new ProducesResponseTypeMetadata(400, typeof(string)),
            new ProducesResponseTypeMetadata(400, typeof(int))
        ];
        var selected = new ApiResponseType { StatusCode = 400, Type = typeof(int) };
        selected.ApiResponseFormats.Add(new() { MediaType = "application/json" });
        selected.ApiResponseFormats.Add(new() { MediaType = "text/plain" });
        description.SupportedResponseTypes.Add(selected);
        description.SupportedResponseTypes.Add(new() { StatusCode = 400, Type = typeof(string) });

        Normalize(description);

        description.SupportedResponseTypes.ShouldHaveSingleItem().ShouldBeSameAs(selected);
        selected.ApiResponseFormats.Count.ShouldBe(2);
    }

    [Fact]
    public void default_and_explicit_responses_with_the_same_status_are_distinct()
    {
        var description = new ApiDescription { ActionDescriptor = new() };
        description.SupportedResponseTypes.Add(new() { StatusCode = 400, Type = typeof(string), IsDefaultResponse = true });
        description.SupportedResponseTypes.Add(new() { StatusCode = 400, Type = typeof(int) });

        Normalize(description);

        description.SupportedResponseTypes.Count.ShouldBe(2);
    }

    [Fact]
    public void attribute_metadata_selects_the_declared_type()
    {
        var description = new ApiDescription { ActionDescriptor = new() };
        description.ActionDescriptor.EndpointMetadata = [new Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute(typeof(int), 400)];
        description.SupportedResponseTypes.Add(new() { StatusCode = 400, Type = typeof(int) });
        description.SupportedResponseTypes.Add(new() { StatusCode = 400, Type = typeof(string) });

        Normalize(description);

        description.SupportedResponseTypes.ShouldHaveSingleItem().Type.ShouldBe(typeof(int));
    }

    [Fact]
    public void produces_metadata_takes_precedence_over_attributes()
    {
        var description = new ApiDescription { ActionDescriptor = new() };
        description.ActionDescriptor.EndpointMetadata =
        [
            new ProducesResponseTypeMetadata(400, typeof(int)),
            new Microsoft.AspNetCore.Mvc.ProducesResponseTypeAttribute(typeof(string), 400)
        ];
        description.SupportedResponseTypes.Add(new() { StatusCode = 400, Type = typeof(int) });
        description.SupportedResponseTypes.Add(new() { StatusCode = 400, Type = typeof(string) });

        Normalize(description);

        description.SupportedResponseTypes.ShouldHaveSingleItem().Type.ShouldBe(typeof(int));
    }

    [Fact]
    public void multiple_documents_register_compatibility_only_once_on_net11()
    {
        var services = new ServiceCollection();
        services.OpenApiDocument(o => o.DocumentName = "first");
        services.OpenApiDocument(o => o.DocumentName = "second");

#if NET11_0_OR_GREATER
        const int expectedCount = 1;
#else
        const int expectedCount = 0;
#endif
        services.Count(d => d.ImplementationType == typeof(ResponseTypeCompatibilityProvider)).ShouldBe(expectedCount);
    }

    static void Normalize(ApiDescription description)
    {
        var context = new ApiDescriptionProviderContext([]);
        context.Results.Add(description);
        new ResponseTypeCompatibilityProvider().OnProvidersExecuted(context);
    }
}
