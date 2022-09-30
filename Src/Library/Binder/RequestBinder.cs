using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    private static QueryPropCacheEntry? fromQueryParamsProp;
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
            if (!propInfo.CanWrite || !propInfo.CanRead)
                continue;

            if (isPlainTextRequest && propInfo.Name == nameof(IPlainTextRequest.Content))
                continue; //allow other properties other than `Content` property if this is a plaintext request

            string? fieldName = null;
            var addPrimary = true;
            var compiledSetter = tRequest.SetterForProp(propInfo.Name);

            foreach (var att in propInfo.GetCustomAttributes()) //reduce allocations by doing this only once
            {
                switch (att)
                {
                    case FromBodyAttribute:
                        if (fromBodyProp is not null) throw new InvalidOperationException($"Only one [FromBody] attribute is allowed on [{tRequest.FullName}].");
                        addPrimary = SetFromBodyPropCache(propInfo, compiledSetter);
                        break;

                    case FromQueryParamsAttribute:
                        if (fromQueryParamsProp is not null) throw new InvalidOperationException($"Only one [FromQueryParams] attribute is allowed on [{tRequest.FullName}].");
                        addPrimary = SetFromQueryParamsPropCache(propInfo, compiledSetter);
                        break;

                    case FromClaimAttribute fcAtt:
                        addPrimary = AddFromClaimPropCacheEntry(fcAtt, propInfo, compiledSetter);
                        break;

                    case FromHeaderAttribute fhAtt:
                        addPrimary = AddFromHeaderPropCacheEntry(fhAtt, propInfo, compiledSetter);
                        break;

                    case HasPermissionAttribute hpAtt:
                        addPrimary = AddHasPermissionPropCacheEntry(hpAtt, propInfo, compiledSetter);
                        break;

                    case BindFromAttribute bfAtt:
                        fieldName = bfAtt.Name;
                        break;
                }
            }

            if (addPrimary)
                AddPrimaryPropCacheEntry(fieldName, propInfo, compiledSetter);
        }
    }

    /// <summary>
    /// override this method to customize the request binding logic
    /// </summary>
    /// <param name="ctx">the request binder context which holds all the data required for binding the incoming request</param>
    /// <param name="cancellation">cancellation token</param>
    /// <exception cref="ValidationFailureException">thrown if any failures occur during the binding process</exception>
    public async virtual ValueTask<TRequest> BindAsync(BinderContext ctx, CancellationToken cancellation)
    {
        if (skipModelBinding)
            return new TRequest();

        var req = !isPlainTextRequest && ctx.HttpContext.Request.HasJsonContentType()
                  ? await BindJsonBody(ctx.HttpContext.Request, ctx.JsonSerializerContext, cancellation)
                  : isPlainTextRequest
                    ? await BindPlainTextBody(ctx.HttpContext.Request.Body)
                    : new TRequest();

        BindFormValues(req, ctx.HttpContext.Request, ctx.ValidationFailures, ctx.DontAutoBindForms);
        BindRouteValues(req, ctx.HttpContext.Request.RouteValues, ctx.ValidationFailures);
        BindQueryParams(req, ctx.HttpContext.Request.Query, ctx.ValidationFailures);
        BindUserClaims(req, ctx.HttpContext.User.Claims, ctx.ValidationFailures);
        BindHeaders(req, ctx.HttpContext.Request.Headers, ctx.ValidationFailures);
        BindHasPermissionProps(req, ctx.HttpContext.User.Claims, ctx.ValidationFailures);

        return ctx.ValidationFailures.Count == 0
               ? req
               : throw new ValidationFailureException(ctx.ValidationFailures, "Model binding failed!");
    }

    private static async ValueTask<TRequest> BindJsonBody(HttpRequest httpRequest, JsonSerializerContext? serializerCtx, CancellationToken cancellation)
    {
        if (fromBodyProp is null)
            return (TRequest)(await SerOpts.RequestDeserializer(httpRequest, tRequest, serializerCtx, cancellation))! ?? new();

        var req = new TRequest();

        fromBodyProp.PropSetter(
            req,
            (await SerOpts.RequestDeserializer(httpRequest, fromBodyProp.PropType, serializerCtx, cancellation))!);

        return req;
    }

    private static async ValueTask<TRequest> BindPlainTextBody(Stream body)
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

        if (fromQueryParamsProp is null)
        {
            foreach (var kvp in query)
                Bind(req, kvp, failures);
        }
        else
        {
            var obj = new JsonObject(new() { PropertyNameCaseInsensitive = true });

            foreach (var kvp in query)
            {
                if (!fromQueryParamsProp.Properties.TryGetValue(kvp.Key, out var type))
                {
                    Bind(req, kvp, failures);
                    continue;
                }
                var parser = type.QueryValueParser();
                var startIndex = kvp.Key.IndexOf('[');
                if (startIndex > 0 && kvp.Key[^1] == ']')
                {
                    var key = kvp.Key[..startIndex];
                    if (!obj.ContainsKey(key)) obj[key] = new JsonObject();
                    var nestedProps = kvp.Key.Substring(startIndex + 1, kvp.Key.Length - startIndex - 2).Split("][");
                    obj[key]!.GetOrCreateLastNode(nestedProps)[nestedProps[^1]] = parser(kvp.Value);
                }
                else
                {
                    obj[kvp.Key] = parser(kvp.Value);
                }
            }

            fromQueryParamsProp.PropSetter(req, obj.Deserialize(fromQueryParamsProp.PropType, SerOpts.Options)!);
        }
    }

    private static void BindUserClaims(TRequest req, IEnumerable<Claim> claims, List<ValidationFailure> failures)
    {
        for (var i = 0; i < fromClaimProps.Count; i++)
        {
            var prop = fromClaimProps[i];
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
        for (var i = 0; i < fromHeaderProps.Count; i++)
        {
            var prop = fromHeaderProps[i];
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
        for (var i = 0; i < hasPermissionProps.Count; i++)
        {
            var prop = hasPermissionProps[i];
            var hasPerm = claims.Any(c =>
               string.Equals(c.Type, SecOpts.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
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

    private static bool AddFromClaimPropCacheEntry(FromClaimAttribute att, PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        fromClaimProps.Add(new()
        {
            Identifier = att.ClaimType ?? propInfo.Name,
            ForbidIfMissing = att.IsRequired,
            PropType = propInfo.PropertyType,
            IsCollection = propInfo.PropertyType != Types.String && propInfo.PropertyType.GetInterfaces().Contains(Types.IEnumerable),
            ValueParser = propInfo.PropertyType.ValueParser(),
            PropSetter = compiledSetter,
        });

        return !att.IsRequired; //if claim is optional, return true so it will also be added as a PropCacheEntry
    }

    private static bool AddFromHeaderPropCacheEntry(FromHeaderAttribute att, PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        fromHeaderProps.Add(new()
        {
            Identifier = att.HeaderName ?? propInfo.Name,
            ForbidIfMissing = att.IsRequired,
            PropType = propInfo.PropertyType,
            ValueParser = propInfo.PropertyType.ValueParser(),
            PropSetter = compiledSetter
        });

        return !att.IsRequired; //if header is optional, return true so it will also be added as a PropCacheEntry;
    }

    private static bool AddHasPermissionPropCacheEntry(HasPermissionAttribute att, PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        hasPermissionProps.Add(new()
        {
            Identifier = att.Permission ?? propInfo.Name,
            ForbidIfMissing = att.IsRequired,
            PropType = propInfo.PropertyType,
            PropName = propInfo.Name,
            ValueParser = propInfo.PropertyType.ValueParser(),
            PropSetter = compiledSetter
        });

        return false; // don't allow binding from any other sources
    }

    private static void AddPrimaryPropCacheEntry(string? fieldName, PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        primaryProps.Add(fieldName ?? propInfo.Name, new()
        {
            PropType = propInfo.PropertyType,
            ValueParser = propInfo.PropertyType.ValueParser(),
            PropSetter = compiledSetter
        });
    }

    private static bool SetFromBodyPropCache(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        fromBodyProp = new()
        {
            PropType = propInfo.PropertyType,
            PropSetter = compiledSetter,
        };
        return false;
    }

    private static bool SetFromQueryParamsPropCache(PropertyInfo propInfo, Action<object, object> compiledSetter)
    {
        fromQueryParamsProp = new()
        {
            PropType = propInfo.PropertyType,
            PropSetter = compiledSetter,
            Properties = GetExpectedQueryParams(propInfo.PropertyType.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy), null)
        };
        return false;
    }

    private static Dictionary<string, Type> GetExpectedQueryParams(PropertyInfo[] propertyInfos, string? parentName)
    {
        var dictionary = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in propertyInfos)
        {
            var propName = parentName == null ? prop.Name : $"{parentName}[{prop.Name}]";
            var type = prop.PropertyType;
            var nestedProps = type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy);

            if (!type.IsEnum &&
                 type != Types.String &&
                 type != Types.Bool &&
                !type.GetInterfaces().Contains(Types.IEnumerable) &&
                 nestedProps.Length > 0)
            {
                foreach (var nestedProp in GetExpectedQueryParams(nestedProps, propName))
                    dictionary.Add(nestedProp.Key, nestedProp.Value);
            }
            else
            {
                dictionary.Add(propName, type);
            }
        }
        return dictionary;
    }
}