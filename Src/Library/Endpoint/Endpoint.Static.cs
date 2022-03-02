using FastEndpoints.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using System.Text.Json.Serialization;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new() where TResponse : notnull, new()
{
    private static async Task<TRequest> BindToModel(HttpContext ctx, List<ValidationFailure> failures, JsonSerializerContext? serializerCtx, CancellationToken cancellation)
    {
        TRequest? req = default;

        if (ctx.Request.ContentLength != 0)
        {
            if (isPlainTextRequest)
                req = await BindPlainTextBody(ctx.Request.Body).ConfigureAwait(false);
            else if (ctx.Request.HasJsonContentType())
                req = (TRequest?)await FastEndpoints.Config.ReqDeserializerFunc(ctx.Request, tRequest, serializerCtx, cancellation);
        }

        if (req is null)
            req = new();

        BindFormValues(req, ctx.Request, failures);
        BindRouteValues(req, ctx.Request.RouteValues, failures);
        BindQueryParams(req, ctx.Request.Query, failures);
        BindUserClaims(req, ctx.User.Claims, failures);
        BindHeaders(req, ctx.Request.Headers, failures);
        BindHasPermissionProps(req, ctx.User.Claims, failures);

        if (failures.Count > 0) throw new ValidationFailureException();

        return req;
    }

    private static async Task ValidateRequest(TRequest req, HttpContext ctx, EndpointDefinition ep, object? preProcessors, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (ep.ValidatorType is null)
            return;

        var validator = (IValidator<TRequest>)ctx.RequestServices.GetRequiredService(ep.ValidatorType)!;

        var valResult = await validator.ValidateAsync(req, cancellation).ConfigureAwait(false);

        if (!valResult.IsValid)
            validationFailures.AddRange(valResult.Errors);

        if (validationFailures.Count > 0 && ep.ThrowIfValidationFails)
        {
            await RunPreprocessors(preProcessors, req, ctx, validationFailures, cancellation).ConfigureAwait(false);
            throw new ValidationFailureException();
        }
    }

    private static async Task RunPostProcessors(object? postProcessors, TRequest req, TResponse resp, HttpContext ctx, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (postProcessors is not null)
        {
            foreach (var pp in (IPostProcessor<TRequest, TResponse>[])postProcessors)
                await pp.PostProcessAsync(req, resp, ctx, validationFailures, cancellation).ConfigureAwait(false);
        }
    }

    private static async Task RunPreprocessors(object? preProcessors, TRequest req, HttpContext ctx, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (preProcessors is not null)
        {
            foreach (var p in (IPreProcessor<TRequest>[])preProcessors)
                await p.PreProcessAsync(req, ctx, validationFailures, cancellation).ConfigureAwait(false);
        }
    }

    private static async Task<TRequest> BindPlainTextBody(Stream body)
    {
        IPlainTextRequest req = (IPlainTextRequest)new TRequest();
        using var streamReader = new StreamReader(body);
        req.Content = await streamReader.ReadToEndAsync().ConfigureAwait(false);
        return (TRequest)req;
    }

    private static Task SendReponseIfNotSent(HttpContext ctx, TResponse? responseDto, JsonSerializerContext? jsonSerializerContext, CancellationToken cancellation)
    {
        if (!ctx.Response.HasStarted)
        {
            if (responseDto is null)
                return ctx.Response.SendNoContentAsync(cancellation);
            else
                return ctx.Response.SendAsync(responseDto, 200, jsonSerializerContext, cancellation);
        }
        return Task.CompletedTask;
    }

    private static void BindFormValues(TRequest req, HttpRequest httpRequest, List<ValidationFailure> failures)
    {
        if (!httpRequest.HasFormContentType) return;

        var formFields = httpRequest.Form.Select(kv => new KeyValuePair<string, object?>(kv.Key, kv.Value[0])).ToArray();

        for (int x = 0; x < formFields.Length; x++)
            Bind(req, formFields[x], failures);

        for (int y = 0; y < httpRequest.Form.Files.Count; y++)
        {
            var formFile = httpRequest.Form.Files[y];

            if (ReqTypeCache<TRequest>.CachedProps.TryGetValue(formFile.Name, out var prop))
            {
                if (prop.PropType == Types.IFormFile)
                    prop.PropSetter(req, formFile);
                else
                    failures.Add(new(formFile.Name, "Files can only be bound to properties of type IFormFile!"));
            }
        }
    }

    private static void BindRouteValues(TRequest req, RouteValueDictionary routeValues, List<ValidationFailure> failures)
    {
        if (routeValues.Count == 0) return;

        foreach (var kvp in routeValues)
        {
            if ((kvp.Value as string)?.StartsWith("{") is false)
                Bind(req, kvp, failures);
        }
    }

    private static void BindQueryParams(TRequest req, IQueryCollection query, List<ValidationFailure> failures)
    {
        if (query.Count == 0) return;

        foreach (var kvp in query)
            Bind(req, new(kvp.Key, kvp.Value[0]), failures);
    }

    private static void BindUserClaims(TRequest req, IEnumerable<Claim> claims, List<ValidationFailure> failures)
    {
        var cachedProps = ReqTypeCache<TRequest>.CachedFromClaimProps;

        for (int i = 0; i < cachedProps.Count; i++)
        {
            var prop = cachedProps[i];

            string? claimVal = null;
            foreach (var c in claims)
            {
                if (c.Type.Equals(prop.Identifier, StringComparison.OrdinalIgnoreCase))
                {
                    claimVal = c.Value;
                    break;
                }
            }

            if (claimVal is null && prop.ForbidIfMissing)
                failures.Add(new(prop.Identifier, "User doesn't have this claim type!"));

            if (claimVal is not null && prop.ValueParser is not null)
            {
                var (success, value) = prop.ValueParser(claimVal);
                prop.PropSetter(req, value);

                if (!success)
                    failures.Add(new(prop.Identifier, $"Unable to bind claim value [{claimVal}] to a [{prop.PropType.Name}] property!"));
            }
        }
    }

    private static void BindHeaders(TRequest req, IHeaderDictionary headers, List<ValidationFailure> failures)
    {
        var cachedProps = ReqTypeCache<TRequest>.CachedFromHeaderProps;

        for (int i = 0; i < cachedProps.Count; i++)
        {
            var prop = cachedProps[i];
            var hdrVal = headers[prop.Identifier].FirstOrDefault();

            if (hdrVal is null && prop.ForbidIfMissing)
                failures.Add(new(prop.Identifier, "This header is missing from the request!"));

            if (hdrVal is not null && prop.ValueParser is not null)
            {
                var (success, value) = prop.ValueParser(hdrVal);
                prop.PropSetter(req, value);

                if (!success)
                    failures.Add(new(prop.Identifier, $"Unable to bind header value [{hdrVal}] to a [{prop.PropType.Name}] property!"));
            }
        }
    }

    private static void BindHasPermissionProps(TRequest req, IEnumerable<Claim> claims, List<ValidationFailure> failures)
    {
        var cachedProps = ReqTypeCache<TRequest>.CachedHasPermissionProps;

        for (int i = 0; i < cachedProps.Count; i++)
        {
            var prop = cachedProps[i];

            bool hasPerm = claims.Any(c =>
               string.Equals(c.Type, Constants.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(c.Value, prop.Identifier, StringComparison.OrdinalIgnoreCase));

            if (!hasPerm && prop.ForbidIfMissing)
                failures.Add(new(prop.Identifier, "User doesn't have this permission!"));

            if (hasPerm && prop.ValueParser is not null)
            {
                var (success, value) = prop.ValueParser(hasPerm);
                prop.PropSetter(req, value);

                if (!success)
                    failures.Add(new(prop.PropName, $"Attribute [HasPermission] does not work with [{prop.PropType.Name}] properties!"));
            }
        }
    }

    private static void Bind(TRequest req, KeyValuePair<string, object?> kvp, List<ValidationFailure> failures)
    {
        if (ReqTypeCache<TRequest>.CachedProps.TryGetValue(kvp.Key, out var prop) && prop.ValueParser is not null)
        {
            var (success, value) = prop.ValueParser(kvp.Value);
            prop.PropSetter(req, value);

            if (!success)
                failures.Add(new(kvp.Key, $"Unable to bind [{kvp.Value}] to a [{prop.PropType.Name}] property!"));
        }
    }
}