using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc.ApiExplorer;

namespace FastEndpoints.OpenApi;

// Preserve last-declaration-wins when API Explorer reports several CLR types for a status.
sealed class ResponseTypeCompatibilityProvider : IApiDescriptionProvider
{
    public int Order => int.MinValue;

    public void OnProvidersExecuting(ApiDescriptionProviderContext context) { }

    public void OnProvidersExecuted(ApiDescriptionProviderContext context)
    {
        foreach (var description in context.Results)
        {
            var responses = description.SupportedResponseTypes;
            var groups = responses.GroupBy(r => (r.StatusCode, r.IsDefaultResponse)).ToArray();

            if (groups.Length == responses.Count)
                continue;

            var normalized = groups.Select(
                group =>
                {
                    // Retain the existing last-declaration-wins policy, not API Explorer's CLR-type sort order.
                    var metadata = description.ActionDescriptor.EndpointMetadata;
                    var type = metadata.OfType<IProducesResponseTypeMetadata>().LastOrDefault(m => m.StatusCode == group.Key.StatusCode)?.Type
                               ?? metadata.OfType<IApiResponseMetadataProvider>().LastOrDefault(m => m.StatusCode == group.Key.StatusCode)?.Type;

                    return group.LastOrDefault(r => r.Type == type) ?? group.Last();
                }).ToArray();

            responses.Clear();

            foreach (var response in normalized)
                responses.Add(response);
        }
    }
}
