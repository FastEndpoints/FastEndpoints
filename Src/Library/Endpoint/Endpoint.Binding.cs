using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System.Security.Claims;
using System.Text.Json.Serialization;
using static FastEndpoints.Config;
using static FastEndpoints.Constants;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new() where TResponse : notnull
{
    private static async Task<TRequest> BindToModel(HttpContext ctx, List<ValidationFailure> failures, JsonSerializerContext? serializerCtx, bool dontAutoBindForm, CancellationToken cancellation)
    {
        if (skipModelBinding)
            return new TRequest();

        var req =
            ctx.Request.HasJsonContentType()
            ? await BindJsonBody(ctx, serializerCtx, cancellation)
            : isPlainTextRequest
              ? await BindPlainTextBody(ctx.Request.Body)
              : new TRequest();

        BindFormValues(req, ctx.Request, failures, dontAutoBindForm);
        BindRouteValues(req, ctx.Request.RouteValues, failures);
        BindQueryParams(req, ctx.Request.Query, failures);
        BindUserClaims(req, ctx.User.Claims, failures);
        BindHeaders(req, ctx.Request.Headers, failures);
        BindHasPermissionProps(req, ctx.User.Claims, failures);

        return failures.Count == 0
               ? req
               : throw new ValidationFailureException(failures, "Model binding failed");
    }

    private static async Task<TRequest> BindJsonBody(HttpContext ctx, JsonSerializerContext? serializerCtx, CancellationToken cancellation)
    {
        if (bindFromBodyProp is null)
            return (TRequest?)await ReqDeserializerFunc(ctx.Request, tRequest, serializerCtx, cancellation) ?? new();

        var req = new TRequest();

        bindFromBodyProp.PropSetter(
            req,
            (await ReqDeserializerFunc(ctx.Request, bindFromBodyProp.PropType, serializerCtx, cancellation))!);

        return req;
    }

    private static async Task<TRequest> BindPlainTextBody(Stream body)
    {
        var req = (IPlainTextRequest)new TRequest();
        using var streamReader = new StreamReader(body);
        req.Content = await streamReader.ReadToEndAsync();
        return (TRequest)req;
    }

    private static void BindFormValues(TRequest req, HttpRequest httpRequest, List<ValidationFailure> failures, bool dontAutoBindForm)
    {
        if (!httpRequest.HasFormContentType || dontAutoBindForm) return;

        foreach (var kvp in httpRequest.Form)
            Bind(req, kvp, failures);

        for (var y = 0; y < httpRequest.Form.Files.Count; y++)
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
            var val = kvp.Value?.ToString();
            if (val?.StartsWith("{") is false)
                Bind(req, new(kvp.Key, val), failures);
        }
    }

    private static void BindQueryParams(TRequest req, IQueryCollection query, List<ValidationFailure> failures)
    {
        if (query.Count == 0) return;

        foreach (var kvp in query)
            Bind(req, kvp, failures);
    }

    private static void BindUserClaims(TRequest req, IEnumerable<Claim> claims, List<ValidationFailure> failures)
    {
        var cachedProps = ReqTypeCache<TRequest>.CachedFromClaimProps;

        for (var i = 0; i < cachedProps.Count; i++)
        {
            var prop = cachedProps[i];
            StringValues? claimVal = null;

            foreach (var g in claims.GroupBy(c => c.Type, c => c.Value))
            {
                if (g.Key.Equals(prop.Identifier, StringComparison.OrdinalIgnoreCase))
                {
                    claimVal =
                        prop.IsCollection || g.Count() > 1
                        ? g.Select(v => v).ToArray()
                        : g.FirstOrDefault();
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

        for (var i = 0; i < cachedProps.Count; i++)
        {
            var prop = cachedProps[i];
            var hdrVal = headers[prop.Identifier];

            if (hdrVal.Count == 0 && prop.ForbidIfMissing)
                failures.Add(new(prop.Identifier, "This header is missing from the request!"));

            if (hdrVal.Count > 0 && prop.ValueParser is not null)
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

        for (var i = 0; i < cachedProps.Count; i++)
        {
            var prop = cachedProps[i];

            var hasPerm = claims.Any(c =>
               string.Equals(c.Type, PermsClaimType, StringComparison.OrdinalIgnoreCase) &&
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

    private static void Bind(TRequest req, KeyValuePair<string, StringValues> kvp, List<ValidationFailure> failures)
    {
        if (ReqTypeCache<TRequest>.CachedProps.TryGetValue(kvp.Key, out var prop) && prop.ValueParser is not null)
        {
            var (success, value) = prop.ValueParser(kvp.Value);

            if (success)
                prop.PropSetter(req, value);
            else
                failures.Add(new(kvp.Key, $"Unable to bind [{kvp.Value}] to a [{prop.PropType.ActualName()}] property!"));
        }
    }
}
