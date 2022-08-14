using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json.Serialization;
using static FastEndpoints.Config;

namespace FastEndpoints;

/// <summary>
/// the default request binder for a given request dto type
/// </summary>
/// <typeparam name="TRequest">the type of the request dto this binder will be dealing with</typeparam>
public class RequestBinder<TRequest> : IRequestBinder<TRequest> where TRequest : notnull, new()
{
    private static readonly Type tRequest = typeof(TRequest);
    private static readonly bool isPlainTextRequest = Types.IPlainTextRequest.IsAssignableFrom(tRequest);
    private static readonly bool skipModelBinding = tRequest == Types.EmptyRequest && !isPlainTextRequest;
    private static PropCache? fromBodyProp;
    private static readonly Dictionary<string, PrimaryPropCacheEntry> primaryProps = new(StringComparer.OrdinalIgnoreCase); //key: property name
    private static readonly List<SecondaryPropCacheEntry> fromClaimProps = new();
    private static readonly List<SecondaryPropCacheEntry> fromHeaderProps = new();
    private static readonly List<SecondaryPropCacheEntry> hasPermissionProps = new();

    static RequestBinder()
    {
        if (skipModelBinding)
            return;

        // if the request dto type is an IEnumerable such as List<T>, it will be deserialized by STJ.
        // so skip setup for this dto type.
        // otherwise, a request dto such as MyRequest<T> can have a value parser, so allow to proceed.
        if (tRequest.IsGenericType && tRequest.GetInterfaces().Contains(Types.IEnumerable))
            return;

        foreach (var propInfo in tRequest.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy))
        {
            if (!propInfo.CanRead || !propInfo.CanWrite)
                continue;

            if (isPlainTextRequest && propInfo.Name == nameof(IPlainTextRequest.Content))
                continue;

            var compiledSetter = tRequest.SetterForProp(propInfo.Name);

            if (SetFromBodyPropCache(propInfo, compiledSetter))
                continue;

            if (AddFromClaimPropCacheEntry(propInfo, compiledSetter))
                continue;

            if (AddFromHeaderPropCacheEntry(propInfo, compiledSetter))
                continue;

            if (AddHasPermissionPropCacheEntry(propInfo, compiledSetter))
                continue;

            AddPropCacheEntry(propInfo, compiledSetter);
        }
    }

    /// <summary>
    /// override this method to customize the request binding logic
    /// </summary>
    /// <param name="context">the request binder context which holds all the data required for binding the incoming request</param>
    /// <param name="cancellation">cancellation token</param>
    /// <exception cref="ValidationFailureException">thrown if any failures occur during the binding process</exception>
    public async virtual ValueTask<TRequest> BindAsync(BinderContext context, CancellationToken cancellation)
    {
        if (skipModelBinding)
            return new TRequest();

        var req =
            context.HttpContext.Request.HasJsonContentType()
            ? await BindJsonBody(context.HttpContext.Request, context.JsonSerializerContext, cancellation)
            : isPlainTextRequest
              ? await BindPlainTextBody(context.HttpContext.Request.Body)
              : new TRequest();

        BindFormValues(req, context.HttpContext.Request, context.ValidationFailures, context.DontAutoBindForms);
        BindRouteValues(req, context.HttpContext.Request.RouteValues, context.ValidationFailures);
        BindQueryParams(req, context.HttpContext.Request.Query, context.ValidationFailures);
        BindUserClaims(req, context.HttpContext.User.Claims, context.ValidationFailures);
        BindHeaders(req, context.HttpContext.Request.Headers, context.ValidationFailures);
        BindHasPermissionProps(req, context.HttpContext.User.Claims, context.ValidationFailures);

        return context.ValidationFailures.Count == 0
               ? req
               : throw new ValidationFailureException(context.ValidationFailures, "Model binding failed");
    }

    private static async Task<TRequest> BindJsonBody(HttpRequest httpRequest, JsonSerializerContext? serializerCtx, CancellationToken cancellation)
    {
        if (fromBodyProp is null)
            return (TRequest?)await ReqDeserializerFunc(httpRequest, tRequest, serializerCtx, cancellation) ?? new();

        var req = new TRequest();

        fromBodyProp.PropSetter(
            req,
            (await ReqDeserializerFunc(httpRequest, fromBodyProp.PropType, serializerCtx, cancellation))!);

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

            if (primaryProps.TryGetValue(formFile.Name, out var prop))
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
        var cachedProps = fromClaimProps;

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
        var cachedProps = fromHeaderProps;

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
        var cachedProps = hasPermissionProps;

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
        if (primaryProps.TryGetValue(kvp.Key, out var prop) && prop.ValueParser is not null)
        {
            var (success, value) = prop.ValueParser(kvp.Value);

            if (success)
                prop.PropSetter(req, value);
            else
                failures.Add(new(kvp.Key, $"Unable to bind [{kvp.Value}] to a [{prop.PropType.ActualName()}] property!"));
        }
    }

    private static bool SetFromBodyPropCache(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<FromBodyAttribute>(false);
        if (attrib is not null)
        {
            fromBodyProp = new()
            {
                PropType = propInfo.PropertyType,
                PropSetter = compiledSetter,
            };
            return true;
        }
        return false;
    }

    private static bool AddFromClaimPropCacheEntry(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<FromClaimAttribute>(false);
        if (attrib is not null)
        {
            var claimType = attrib.ClaimType ?? propInfo.Name;
            var forbidIfMissing = attrib.IsRequired;

            fromClaimProps.Add(new()
            {
                Identifier = claimType,
                ForbidIfMissing = forbidIfMissing,
                PropType = propInfo.PropertyType,
                IsCollection = propInfo.PropertyType != Types.String && propInfo.PropertyType.GetInterfaces().Contains(Types.IEnumerable),
                ValueParser = propInfo.PropertyType.ValueParser(),
                PropSetter = compiledSetter,
            });

            return forbidIfMissing; //if claim is optional, return false so it will also be added as a PropCacheEntry
        }
        return false;
    }

    private static bool AddFromHeaderPropCacheEntry(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<FromHeaderAttribute>(false);
        if (attrib is not null)
        {
            var headerName = attrib.HeaderName ?? propInfo.Name;
            var forbidIfMissing = attrib.IsRequired;

            fromHeaderProps.Add(new()
            {
                Identifier = headerName,
                ForbidIfMissing = forbidIfMissing,
                PropType = propInfo.PropertyType,
                ValueParser = propInfo.PropertyType.ValueParser(),
                PropSetter = compiledSetter
            });

            return forbidIfMissing; //if header is optional, return false so it will also be added as a PropCacheEntry;
        }
        return false;
    }

    private static bool AddHasPermissionPropCacheEntry(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<HasPermissionAttribute>(false);
        if (attrib is not null)
        {
            var permission = attrib.Permission ?? propInfo.Name;
            var forbidIfMissing = attrib.IsRequired;

            hasPermissionProps.Add(new()
            {
                Identifier = permission,
                ForbidIfMissing = forbidIfMissing,
                PropType = propInfo.PropertyType,
                PropName = propInfo.Name,
                ValueParser = propInfo.PropertyType.ValueParser(),
                PropSetter = compiledSetter
            });

            return true; // don't allow binding from any other sources
        }
        return false;
    }

    private static void AddPropCacheEntry(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        var attrib = propInfo.GetCustomAttribute<BindFromAttribute>(false);

        primaryProps.Add(attrib?.Name ?? propInfo.Name, new()
        {
            PropType = propInfo.PropertyType,
            ValueParser = propInfo.PropertyType.ValueParser(),
            PropSetter = compiledSetter
        });
    }
}
