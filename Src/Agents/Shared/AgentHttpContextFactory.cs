using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using static FastEndpoints.Config;

namespace FastEndpoints.Agents;

sealed class AgentHttpContextFactory
{
    readonly AgentRequestBuilder _requestBuilder = new();

    public DefaultHttpContext Create(EndpointDefinition definition,
                                     JsonElement args,
                                     System.Security.Claims.ClaimsPrincipal? principal,
                                     IServiceProvider services,
                                     CancellationToken ct)
    {
        var bindingSerializerOptions = definition.SerializerContext?.Options ?? SerOpts.Options;
        var request = _requestBuilder.Build(definition, args, bindingSerializerOptions, SerOpts.Options);
        var ctx = new DefaultHttpContext { RequestServices = services };

        if (principal is not null)
            ctx.User = principal;

        var requestBody = new MemoryStream();

        if (request.Body is { } body)
        {
            using var writer = new Utf8JsonWriter(requestBody);
            body.WriteTo(writer);
            writer.Flush();
            ctx.Request.ContentType = "application/json";
        }

        requestBody.Position = 0;

        ctx.Request.Body = requestBody;
        ctx.Request.ContentLength = requestBody.Length;
        ctx.RequestAborted = ct;
        ctx.Request.Method = request.Method;
        ctx.Request.Path = request.Path;
        var routeValues = new RouteValueDictionary(request.RouteValues);
        ctx.Features.Set<IRouteValuesFeature>(new RouteValuesFeature { RouteValues = routeValues });
        ctx.Request.RouteValues = routeValues;

        if (request.Query.Count > 0)
        {
            ctx.Request.QueryString = QueryString.Create(request.Query);
            ctx.Request.Query = new QueryCollection(
                request.Query.GroupBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                       .ToDictionary(g => g.Key, g => new StringValues(g.Select(x => x.Value).ToArray()), StringComparer.OrdinalIgnoreCase));
        }

        foreach (var (key, value) in request.Headers)
            ctx.Request.Headers[key] = value;

        if (request.Cookies.Count > 0)
            ctx.Request.Headers.Cookie = string.Join("; ", request.Cookies.Select(c => $"{Uri.EscapeDataString(c.Key)}={Uri.EscapeDataString(c.Value ?? string.Empty)}"));

        ctx.Response.Body = new MemoryStream();

        var endpointFeature = new AgentEndpointFeature(definition);
        ctx.Features.Set<IEndpointFeature>(endpointFeature);

        return ctx;
    }

    sealed class AgentEndpointFeature : IEndpointFeature
    {
        public AgentEndpointFeature(EndpointDefinition definition)
        {
            var metadata = new EndpointMetadataCollection(definition);
            Endpoint = new(_ => Task.CompletedTask, metadata, definition.EndpointType.FullName);
        }

        public Endpoint? Endpoint { get; set; }
    }

    sealed class RouteValuesFeature : IRouteValuesFeature
    {
        public RouteValueDictionary RouteValues { get; set; } = [];
    }
}
