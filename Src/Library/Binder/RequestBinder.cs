using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;
using System.Linq.Expressions;
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
public class RequestBinder<TRequest> : IRequestBinder<TRequest> where TRequest : notnull
{
    static readonly Type tRequest = typeof(TRequest);
    static readonly bool isPlainTextRequest = Types.IPlainTextRequest.IsAssignableFrom(tRequest);
    static readonly bool skipModelBinding = tRequest == Types.EmptyRequest && !isPlainTextRequest;
    static PropCache? fromBodyProp;
    static PropCache? fromQueryParamsProp;
    static readonly Dictionary<string, PrimaryPropCacheEntry> primaryProps = new(StringComparer.OrdinalIgnoreCase); //key: property name
    static readonly List<SecondaryPropCacheEntry> fromClaimProps = new();
    static readonly List<SecondaryPropCacheEntry> fromHeaderProps = new();
    static readonly List<SecondaryPropCacheEntry> hasPermissionProps = new();

    static Func<TRequest> _dtoInitializer;
    static Func<TRequest> InitDto => _dtoInitializer ??= CompileDtoInitializer();

    static RequestBinder()
    {
        if (skipModelBinding)
            return;

        // if the request dto type is an IEnumerable such as List<T>, or any class that implements IEnumerable,
        // it will be deserialized by STJ. so skip setup for this dto type.
        // otherwise, a request dto such as MyRequest<T> - which is not IEnumerable can have a value parser, so allow to proceed.
        if (tRequest.GetInterfaces().Contains(Types.IEnumerable))
            return;

        foreach (var propInfo in tRequest.BindableProps())
        {
            if (isPlainTextRequest && propInfo.Name == nameof(IPlainTextRequest.Content))
                continue; //allow other properties other than `Content` property if this is a plaintext request

            foreach (var (matcher, parser) in BindingOptions.PropertyMatchers)
            {
                if (matcher(propInfo))
                {
                    BinderExtensions.ParserFuncCache.TryAdd(
                        propInfo.PropertyType,
                        input => parser(input, propInfo.PropertyType));

                    break;
                }
            }

            string? fieldName = null;
            var addPrimary = true;
            var compiledSetter = tRequest.SetterForProp(propInfo.Name);
            var attribs = Attribute.GetCustomAttributes(propInfo, true);

            for (var i = 0; i < attribs.Length; i++)
            {
                switch (attribs[i])
                {
                    case FromBodyAttribute:
                        if (fromBodyProp is not null)
                            throw new InvalidOperationException($"Only one [FromBody] attribute is allowed on [{tRequest.FullName}].");

                        addPrimary = SetFromBodyPropCache(propInfo, compiledSetter);

                        break;

                    case FromQueryParamsAttribute:
                        if (fromQueryParamsProp is not null)
                            throw new InvalidOperationException($"Only one [FromQueryParams] attribute is allowed on [{tRequest.FullName}].");

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

    readonly bool bindJsonBody;
    readonly bool bindFormFields;
    readonly bool bindRouteValues;
    readonly bool bindQueryParams;
    readonly bool bindUserClaims;
    readonly bool bindHeaders;
    readonly bool bindPermissions;

    /// <summary>
    /// default constructor which enables all binding sources
    /// </summary>
    public RequestBinder()
    {
        bindJsonBody = true;
        bindFormFields = true;
        bindRouteValues = true;
        bindQueryParams = true;
        bindUserClaims = true;
        bindHeaders = true;
        bindPermissions = true;
    }

    /// <summary>
    /// constructor accepting a bitwise combination of enums which enables only the specified binding sources
    /// </summary>
    /// <param name="enabledSources">a bitwise combination of enum values</param>
    public RequestBinder(BindingSource enabledSources)
    {
        bindJsonBody = enabledSources.HasFlag(BindingSource.JsonBody);
        bindFormFields = enabledSources.HasFlag(BindingSource.FormFields);
        bindRouteValues = enabledSources.HasFlag(BindingSource.RouteValues);
        bindQueryParams = enabledSources.HasFlag(BindingSource.QueryParams);
        bindUserClaims = enabledSources.HasFlag(BindingSource.UserClaims);
        bindHeaders = enabledSources.HasFlag(BindingSource.Headers);
        bindPermissions = enabledSources.HasFlag(BindingSource.Permissions);
    }

    /// <summary>
    /// override this method to customize the request binding logic
    /// </summary>
    /// <param name="ctx">the request binder context which holds all the data required for binding the incoming request</param>
    /// <param name="cancellation">cancellation token</param>
    /// <exception cref="ValidationFailureException">thrown if any failures occur during the binding process</exception>
    public virtual async ValueTask<TRequest> BindAsync(BinderContext ctx, CancellationToken cancellation)
    {
        if (skipModelBinding)
            return InitDto();

        var req = !isPlainTextRequest && bindJsonBody && ctx.HttpContext.Request.HasJsonContentType()
                      ? await BindJsonBody(ctx.HttpContext.Request, ctx.JsonSerializerContext, cancellation)
                      : isPlainTextRequest
                          ? await BindPlainTextBody(ctx.HttpContext.Request.Body)
                          : InitDto();

        if (bindFormFields)
            BindFormValues(req, ctx.HttpContext.Request, ctx.ValidationFailures, ctx.DontAutoBindForms);
        if (bindRouteValues)
            BindRouteValues(req, ctx.HttpContext.Request.RouteValues, ctx.ValidationFailures);
        if (bindQueryParams)
            BindQueryParams(req, ctx.HttpContext.Request.Query, ctx.ValidationFailures, ctx.JsonSerializerContext);
        if (bindUserClaims)
            BindUserClaims(req, ctx.HttpContext.User.Claims, ctx.ValidationFailures);
        if (bindHeaders)
            BindHeaders(req, ctx.HttpContext.Request.Headers, ctx.ValidationFailures);
        if (bindPermissions)
            BindHasPermissionProps(req, ctx.HttpContext.User.Claims, ctx.ValidationFailures);

        return ctx.ValidationFailures.Count == 0
                   ? req
                   : throw new ValidationFailureException(ctx.ValidationFailures, "Model binding failed!");
    }

    static async ValueTask<TRequest> BindJsonBody(HttpRequest httpRequest, JsonSerializerContext? serializerCtx, CancellationToken cancellation)
    {
        if (fromBodyProp is null)
            return (TRequest)(await SerOpts.RequestDeserializer(httpRequest, tRequest, serializerCtx, cancellation))! ?? InitDto();

        var req = InitDto();

        fromBodyProp.PropSetter(
            req,
            await SerOpts.RequestDeserializer(httpRequest, fromBodyProp.PropType, serializerCtx, cancellation));

        return req;
    }

    static async ValueTask<TRequest> BindPlainTextBody(Stream body)
    {
        var req = (IPlainTextRequest)InitDto();
        using var streamReader = new StreamReader(body);
        req.Content = await streamReader.ReadToEndAsync();

        return (TRequest)req;
    }

    static void BindFormValues(TRequest req, HttpRequest httpRequest, List<ValidationFailure> failures, bool dontAutoBindForm)
    {
        if (!httpRequest.HasFormContentType || dontAutoBindForm)
            return;

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

    static void BindRouteValues(TRequest req, RouteValueDictionary routeValues, List<ValidationFailure> failures)
    {
        if (routeValues.Count == 0)
            return;

        foreach (var kvp in routeValues)
        {
            var val = kvp.Value?.ToString();
            if (val?.StartsWith("{") is false)
                Bind(req, new(kvp.Key, val), failures);
        }
    }

    static void BindQueryParams(TRequest req, IQueryCollection query, List<ValidationFailure> failures, JsonSerializerContext? serializerCtx)
    {
        if (query.Count == 0)
            return;

        foreach (var kvp in query)
            Bind(req, kvp, failures);

        if (fromQueryParamsProp is not null)
        {
            var obj = new JsonObject(new() { PropertyNameCaseInsensitive = true });
            var sortedDic = new SortedDictionary<string, StringValues>(
                query.ToDictionary(x => x.Key, x => x.Value),
                StringComparer.OrdinalIgnoreCase);
            var swaggerStyle = !sortedDic.Any(x => x.Key.Contains('.') || x.Key.Contains("[0"));

            fromQueryParamsProp.PropType.QueryObjectSetter()(sortedDic, obj, null, null, swaggerStyle);

            fromQueryParamsProp.PropSetter(
                req,
                obj[Constants.QueryJsonNodeName].Deserialize(
                    fromQueryParamsProp.PropType,
                    serializerCtx?.Options ?? SerOpts.Options));
        }
    }

    static void BindUserClaims(TRequest req, IEnumerable<Claim> claims, List<ValidationFailure> failures)
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

            if (claimVal is not null)
            {
                var res = prop.ValueParser(claimVal);
                prop.PropSetter(req, res.Value);

                if (!res.IsSuccess)
                    failures.Add(new(prop.Identifier, $"Unable to bind claim value [{claimVal}] to a [{prop.PropType.Name}] property!"));
            }
        }
    }

    static void BindHeaders(TRequest req, IHeaderDictionary headers, List<ValidationFailure> failures)
    {
        for (var i = 0; i < fromHeaderProps.Count; i++)
        {
            var prop = fromHeaderProps[i];
            var hdrVal = headers[prop.Identifier];

            if (hdrVal.Count == 0 && prop.ForbidIfMissing)
                failures.Add(new(prop.Identifier, "This header is missing from the request!"));

            if (hdrVal.Count > 0)
            {
                var res = prop.ValueParser(hdrVal);
                prop.PropSetter(req, res.Value);

                if (!res.IsSuccess)
                    failures.Add(new(prop.Identifier, $"Unable to bind header value [{hdrVal}] to a [{prop.PropType.Name}] property!"));
            }
        }
    }

    static void BindHasPermissionProps(TRequest req, IEnumerable<Claim> claims, List<ValidationFailure> failures)
    {
        for (var i = 0; i < hasPermissionProps.Count; i++)
        {
            var prop = hasPermissionProps[i];
            var hasPerm = claims.Any(
                c => string.Equals(c.Type, SecOpts.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
                     string.Equals(c.Value, prop.Identifier, StringComparison.OrdinalIgnoreCase));

            switch (hasPerm)
            {
                case false when prop.ForbidIfMissing:
                    failures.Add(new(prop.Identifier, "User doesn't have this permission!"));

                    break;
                case true:
                {
                    var res = prop.ValueParser(hasPerm);
                    prop.PropSetter(req, res.Value);

                    if (!res.IsSuccess)
                        failures.Add(new(prop.PropName, $"Attribute [HasPermission] does not work with [{prop.PropType.Name}] properties!"));

                    break;
                }
            }
        }
    }

    static void Bind(TRequest req, KeyValuePair<string, StringValues> kvp, List<ValidationFailure> failures)
    {
        if (primaryProps.TryGetValue(kvp.Key, out var prop))
        {
            var res = prop.ValueParser(kvp.Value);

            if (res.IsSuccess || IsNullablePropAndInputIsEmptyString(kvp, prop))
                prop.PropSetter(req, res.Value);
            else
                failures.Add(new(kvp.Key, BndOpts.FailureMessage(prop.PropType, kvp.Key, kvp.Value)));
        }

        static bool IsNullablePropAndInputIsEmptyString(KeyValuePair<string, StringValues> kvp, PrimaryPropCacheEntry prop)
            => kvp.Value[0]?.Length == 0 && Nullable.GetUnderlyingType(prop.PropType) != null;
    }

    static bool AddFromClaimPropCacheEntry(FromClaimAttribute att, PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        fromClaimProps.Add(
            new()
            {
                Identifier = att.ClaimType ?? propInfo.Name,
                ForbidIfMissing = att.IsRequired,
                PropType = propInfo.PropertyType,
                IsCollection = propInfo.PropertyType != Types.String && propInfo.PropertyType.GetInterfaces().Contains(Types.IEnumerable),
                ValueParser = propInfo.PropertyType.ValueParser(),
                PropSetter = compiledSetter
            });

        return !att.IsRequired; //if claim is optional, return true so it will also be added as a PropCacheEntry
    }

    static bool AddFromHeaderPropCacheEntry(FromHeaderAttribute att, PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        fromHeaderProps.Add(
            new()
            {
                Identifier = att.HeaderName ?? propInfo.Name,
                ForbidIfMissing = att.IsRequired,
                PropType = propInfo.PropertyType,
                ValueParser = propInfo.PropertyType.ValueParser(),
                PropSetter = compiledSetter
            });

        return !att.IsRequired; //if header is optional, return true so it will also be added as a PropCacheEntry;
    }

    static bool AddHasPermissionPropCacheEntry(HasPermissionAttribute att, PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        hasPermissionProps.Add(
            new()
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

    static void AddPrimaryPropCacheEntry(string? fieldName, PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        primaryProps.Add(
            fieldName ?? propInfo.Name,
            new()
            {
                PropType = propInfo.PropertyType,
                ValueParser = propInfo.PropertyType.ValueParser(),
                PropSetter = compiledSetter
            });
    }

    static bool SetFromBodyPropCache(PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        fromBodyProp = new()
        {
            PropType = propInfo.PropertyType,
            PropSetter = compiledSetter
        };

        return false;
    }

    static bool SetFromQueryParamsPropCache(PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        fromQueryParamsProp = new()
        {
            PropType = propInfo.PropertyType,
            PropSetter = compiledSetter
        };

        return false;
    }

    static Func<TRequest> CompileDtoInitializer()
    {
        var ctor = tRequest
                  .GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                  .MinBy(c => c.GetParameters().Length) ??
                   throw new NotSupportedException(
                       "Only JSON requests (with an \"application/json\" content-type header) can be deserialized to a DTO type without " +
                       $"a constructor! Offending type: [{tRequest.FullName}]");

        var args = ctor.GetParameters();
        var argExpressions = new List<Expression>(args.Length);

        for (var i = 0; i < args.Length; i++)
            argExpressions.Add(Expression.Default(args[i].ParameterType));

        var ctorExpression = Expression.New(ctor, argExpressions);

        return Expression.Lambda<Func<TRequest>>(ctorExpression).Compile();
    }
}