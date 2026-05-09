using System.Text.Json;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
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
    readonly AgentHttpContextFactory _httpContextFactory = new();

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
        DefaultHttpContext httpContext;

        try
        {
            httpContext = _httpContextFactory.Create(definition, args, principal, scope.ServiceProvider, ct);
        }
        catch (UnknownAgentArgumentsException ex)
        {
            return InvocationResult.Invalid(ex.Failures);
        }

        var accessor = scope.ServiceProvider.GetService<IHttpContextAccessor>();
        var resolverAccessor = httpContext.TryResolve<IHttpContextAccessor>();
        var previousContext = accessor?.HttpContext;
        var previousResolverContext = resolverAccessor?.HttpContext;
        BaseEndpoint? endpoint = null;

        try
        {
            if (accessor is not null)
                accessor.HttpContext = httpContext;
            if (resolverAccessor is not null && !ReferenceEquals(resolverAccessor, accessor))
                resolverAccessor.HttpContext = httpContext;

            endpoint = EndpointBootstrap.CreateEndpoint(httpContext, definition);

            await endpoint.ExecAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return InvocationResult.Faulted(ex, endpoint?.ValidationFailures ?? []);
        }
        finally
        {
            if (accessor is not null)
                accessor.HttpContext = previousContext;
            if (resolverAccessor is not null && !ReferenceEquals(resolverAccessor, accessor))
                resolverAccessor.HttpContext = previousResolverContext;

            // ReSharper disable once SuspiciousTypeConversion.Global
            if (endpoint is not null && definition.DisposableAsync)
                await ((IAsyncDisposable)endpoint).DisposeAsync();

            // ReSharper disable once SuspiciousTypeConversion.Global
            if (endpoint is not null && definition.Disposable)
                ((IDisposable)endpoint).Dispose();
        }

        if (endpoint is null)
            throw new InvalidOperationException("Endpoint invocation completed without creating an endpoint instance.");

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