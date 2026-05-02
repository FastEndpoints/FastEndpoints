using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.Agents;

/// <summary>
/// invokes a single <see cref="EndpointDefinition" /> in-process, bypassing HTTP routing. the full
/// FastEndpoints pipeline — binder, validator, pre-/post-processors, <c>ExecuteAsync</c>/<c>HandleAsync</c> —
/// runs exactly as it would for a real request. this is the engine shared by <c>FastEndpoints.Mcp</c>
/// and <c>FastEndpoints.A2A</c>.
/// </summary>
sealed class EndpointInvoker(IServiceScopeFactory scopeFactory)
{
    /// <summary>
    /// invokes <paramref name="definition" /> with <paramref name="args" /> as the request body. the body
    /// is fed to the FastEndpoints binder via a <see cref="DefaultHttpContext" /> whose <c>Request.Body</c>
    /// is a <see cref="MemoryStream" /> containing the JSON payload, so <c>[FromBody]</c>, <c>[FromRoute]</c>,
    /// etc. all resolve naturally when the argument JSON uses the DTO's property names.
    /// </summary>
    /// <param name="definition">the endpoint definition (typically sourced from <c>EndpointData.Found[]</c>).</param>
    /// <param name="args">the raw arguments, treated as a JSON object representing the request dto.</param>
    /// <param name="principal">optional caller identity propagated to <c>HttpContext.User</c>.</param>
    /// <param name="ct">cancellation token forwarded to the endpoint pipeline.</param>
    public async Task<InvocationResult> InvokeAsync(EndpointDefinition definition, JsonElement args, System.Security.Claims.ClaimsPrincipal? principal, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var httpContext = BuildHttpContext(definition, args, principal, scope.ServiceProvider);

        var factory = scope.ServiceProvider.GetRequiredService<IEndpointFactory>();
        var endpoint = factory.Create(definition, httpContext);
        endpoint.Definition = definition;
        endpoint.HttpContext = httpContext;

        try
        {
            await endpoint.ExecAsync(ct);
        }
        catch (Exception ex)
        {
            return InvocationResult.Faulted(ex, endpoint.ValidationFailures);
        }

        // report validation failures regardless of response status: an endpoint that opts into
        // DontThrowIfValidationFails() leaves StatusCode = 200 even when AddError(...) pushed
        // failures onto the collection. gating on StatusCode >= 400 would silently swallow those
        // and hand the agent a "success" response containing failure information it can't see.
        if (endpoint.ValidationFailures.Count > 0)
            return InvocationResult.Invalid(endpoint.ValidationFailures);

        var body = (MemoryStream)httpContext.Response.Body;
        body.Position = 0;
        var payload = body.ToArray();

        return httpContext.Response.StatusCode is >= 200 and < 300
                   ? InvocationResult.Ok(httpContext.Response.StatusCode, httpContext.Response.ContentType, payload)
                   : InvocationResult.HttpError(httpContext.Response.StatusCode, httpContext.Response.ContentType, payload);
    }

    static DefaultHttpContext BuildHttpContext(EndpointDefinition definition, JsonElement args, System.Security.Claims.ClaimsPrincipal? principal, IServiceProvider services)
    {
        var ctx = new DefaultHttpContext { RequestServices = services };

        if (principal is not null)
            ctx.User = principal;

        var requestBody = new MemoryStream();

        if (args.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null)
        {
            using var writer = new Utf8JsonWriter(requestBody);
            args.WriteTo(writer);
        }
        else
            requestBody.Write("{}"u8);
        requestBody.Position = 0;

        ctx.Request.Body = requestBody;
        ctx.Request.ContentType = "application/json";
        ctx.Request.ContentLength = requestBody.Length;
        ctx.Request.Method = "POST";

        var firstRoute = definition.Routes?.FirstOrDefault();
        if (firstRoute is not null)
            ctx.Request.Path = firstRoute.StartsWith('/') ? firstRoute : "/" + firstRoute;

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
}

/// <summary>outcome of <see cref="EndpointInvoker.InvokeAsync" />.</summary>
readonly struct InvocationResult
{
    public InvocationStatus Status { get; }
    public int HttpStatusCode { get; }
    public string? ContentType { get; }
    public byte[] Body { get; }
    public Exception? Exception { get; }
    public IReadOnlyList<ValidationFailure> ValidationFailures { get; }

    InvocationResult(InvocationStatus status, int httpStatusCode, string? contentType, byte[] body, Exception? ex, IReadOnlyList<ValidationFailure> failures)
    {
        Status = status;
        HttpStatusCode = httpStatusCode;
        ContentType = contentType;
        Body = body;
        Exception = ex;
        ValidationFailures = failures;
    }

    public static InvocationResult Ok(int statusCode, string? contentType, byte[] body)
        => new(InvocationStatus.Success, statusCode, contentType, body, null, []);

    public static InvocationResult HttpError(int statusCode, string? contentType, byte[] body)
        => new(InvocationStatus.HttpError, statusCode, contentType, body, null, []);

    public static InvocationResult Invalid(IReadOnlyList<ValidationFailure> failures)
        => new(InvocationStatus.ValidationFailed, 400, null, [], null, failures);

    public static InvocationResult Faulted(Exception ex, IReadOnlyList<ValidationFailure> failures)
        => new(InvocationStatus.Faulted, 500, null, [], ex, failures);
}

enum InvocationStatus
{
    Success,
    HttpError,
    ValidationFailed,
    Faulted
}
