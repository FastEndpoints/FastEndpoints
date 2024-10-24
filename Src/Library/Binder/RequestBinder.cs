using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints;

/// <summary>
/// the default request binder for a given request dto type
/// </summary>
/// <typeparam name="TRequest">the type of the request dto this binder will be dealing with</typeparam>
public class RequestBinder<TRequest> : IRequestBinder<TRequest> where TRequest : notnull
{
    static readonly Type _tRequest = typeof(TRequest);
    static readonly Func<object> _dtoInitializer = _tRequest.ObjectFactory();
    static readonly bool _isPlainTextRequest = Types.IPlainTextRequest.IsAssignableFrom(_tRequest);
    static readonly bool _skipModelBinding = _tRequest == Types.EmptyRequest && !_isPlainTextRequest;
    static PropCache? _fromFormProp;
    static PropCache? _fromBodyProp;
    static PropCache? _fromQueryParamsProp;
    static readonly Dictionary<string, PrimaryPropCacheEntry> _primaryProps = new(StringComparer.OrdinalIgnoreCase); //key: property name
    static readonly Dictionary<string, PropCache> _formFileCollectionProps = new(StringComparer.OrdinalIgnoreCase);
    static readonly List<SecondaryPropCacheEntry> _fromClaimProps = [];
    static readonly List<SecondaryPropCacheEntry> _fromHeaderProps = [];
    static readonly List<SecondaryPropCacheEntry> _hasPermissionProps = [];

    static RequestBinder()
    {
        if (_skipModelBinding)
            return;

        // if the request dto type is an IEnumerable such as List<T>, or any class that implements IEnumerable,
        // it will be deserialized by STJ. so skip setup for this dto type.
        // otherwise, a request dto such as MyRequest<T> - which is not IEnumerable can have a value parser, so allow to proceed.
        if (_tRequest.GetInterfaces().Contains(Types.IEnumerable))
            return;

        var dtoProps = _tRequest.BindableProps();

        if (dtoProps.Count == 0 && !Cfg.EpOpts.AllowEmptyRequestDtos)
        {
            throw new NotSupportedException(
                $"Only request DTOs with publicly accessible properties are supported for request binding. " +
                $"Offending type: [{_tRequest.FullName}]");
        }

        foreach (var prop in dtoProps)
        {
            if (_isPlainTextRequest && prop.Name == nameof(IPlainTextRequest.Content))
                continue; //allow other properties other than `Content` property if this is a plaintext request

            foreach (var (matcher, parser) in BindingOptions.PropertyMatchers)
            {
                if (!matcher(prop))
                    continue;

                BinderExtensions.ParserFuncCache.TryAdd(
                    prop.PropertyType,
                    input => parser(input, prop.PropertyType));

                break;
            }

            string? fieldName = null;
            var addPrimary = true;
            var propSetter = _tRequest.SetterForProp(prop);
            var attribs = Attribute.GetCustomAttributes(prop, true);

            for (var i = 0; i < attribs.Length; i++)
            {
                switch (attribs[i])
                {
                    case FromFormAttribute:
                        if (_fromFormProp is not null)
                            throw new InvalidOperationException($"Only one [FromForm] attribute is allowed on [{_tRequest.FullName}].");

                        addPrimary = SetFromFormPropCache(prop, propSetter);

                        break;

                    case FromBodyAttribute:
                        if (_fromBodyProp is not null)
                            throw new InvalidOperationException($"Only one [FromBody] attribute is allowed on [{_tRequest.FullName}].");

                        addPrimary = SetFromBodyPropCache(prop, propSetter);

                        break;

                    case FromQueryParamsAttribute:
                        if (_fromQueryParamsProp is not null)
                            throw new InvalidOperationException($"Only one [FromQueryParams] attribute is allowed on [{_tRequest.FullName}].");

                        addPrimary = SetFromQueryParamsPropCache(prop, propSetter);

                        break;

                    case FromClaimAttribute fcAtt:
                        addPrimary = AddFromClaimPropCacheEntry(fcAtt, prop, propSetter);

                        break;

                    case FromHeaderAttribute fhAtt:
                        addPrimary = AddFromHeaderPropCacheEntry(fhAtt, prop, propSetter);

                        break;

                    case HasPermissionAttribute hpAtt:
                        addPrimary = AddHasPermissionPropCacheEntry(hpAtt, prop, propSetter);

                        break;

                    case BindFromAttribute bfAtt:
                        fieldName = bfAtt.Name;

                        break;
                }
            }

            if (prop.PropertyType.IsAssignableTo(Types.IEnumerableOfIFormFile))
            {
                AddFormFileCollectionPropCacheEntry(fieldName, prop, propSetter);

                continue;
            }

            {
                if (addPrimary)
                    AddPrimaryPropCacheEntry(fieldName, prop, propSetter);
            }
        }
    }

    readonly bool _bindJsonBody;
    readonly bool _bindFormFields;
    readonly bool _bindRouteValues;
    readonly bool _bindQueryParams;
    readonly bool _bindUserClaims;
    readonly bool _bindHeaders;
    readonly bool _bindPermissions;

    /// <summary>
    /// default constructor which enables all binding sources
    /// </summary>
    public RequestBinder()
    {
        _bindJsonBody = true;
        _bindFormFields = true;
        _bindRouteValues = true;
        _bindQueryParams = true;
        _bindUserClaims = true;
        _bindHeaders = true;
        _bindPermissions = true;
    }

    /// <summary>
    /// constructor accepting a bitwise combination of enums which enables only the specified binding sources
    /// </summary>
    /// <param name="enabledSources">a bitwise combination of enum values</param>
    public RequestBinder(BindingSource enabledSources)
    {
        _bindJsonBody = enabledSources.HasFlag(BindingSource.JsonBody);
        _bindFormFields = enabledSources.HasFlag(BindingSource.FormFields);
        _bindRouteValues = enabledSources.HasFlag(BindingSource.RouteValues);
        _bindQueryParams = enabledSources.HasFlag(BindingSource.QueryParams);
        _bindUserClaims = enabledSources.HasFlag(BindingSource.UserClaims);
        _bindHeaders = enabledSources.HasFlag(BindingSource.Headers);
        _bindPermissions = enabledSources.HasFlag(BindingSource.Permissions);
    }

    /// <summary>
    /// override this method to customize the request binding logic
    /// </summary>
    /// <param name="ctx">the request binder context which holds all the data required for binding the incoming request</param>
    /// <param name="cancellation">cancellation token</param>
    /// <exception cref="ValidationFailureException">thrown if any failures occur during the binding process</exception>
    public virtual async ValueTask<TRequest> BindAsync(BinderContext ctx, CancellationToken cancellation)
    {
        if (_skipModelBinding)
            return (TRequest)_dtoInitializer();

        var req = !_isPlainTextRequest && _bindJsonBody && ctx.HttpContext.Request.HasJsonContentType()
                      ? await BindJsonBody(ctx.HttpContext.Request, ctx.JsonSerializerContext, cancellation)
                      : _isPlainTextRequest
                          ? await BindPlainTextBody(ctx.HttpContext.Request)
                          : (TRequest)_dtoInitializer();

        if (_bindFormFields)
            BindFormValues(req, ctx.HttpContext.Request, ctx.ValidationFailures, ctx.DontAutoBindForms);
        if (_bindRouteValues)
            BindRouteValues(req, ctx.HttpContext.Request.RouteValues, ctx.ValidationFailures);
        if (_bindQueryParams)
            BindQueryParams(req, ctx.HttpContext.Request.Query, ctx.ValidationFailures, ctx.JsonSerializerContext);
        if (_bindUserClaims)
            BindUserClaims(req, ctx.HttpContext.User.Claims, ctx.ValidationFailures);
        if (_bindHeaders)
            BindHeaders(req, ctx.HttpContext.Request.Headers, ctx.ValidationFailures);
        if (_bindPermissions)
            BindHasPermissionProps(req, ctx.HttpContext.User.Claims, ctx.ValidationFailures);

        return ctx.ValidationFailures.Count == 0
                   ? req
                   : throw new ValidationFailureException(ctx.ValidationFailures, "Model binding failed!");
    }

    static async ValueTask<TRequest> BindJsonBody(HttpRequest httpRequest, JsonSerializerContext? serializerCtx, CancellationToken cancellation)
    {
        if (_fromBodyProp is null || httpRequest.Headers.ContainsKey(Constants.RoutelessTest))
            return (TRequest)(await Cfg.SerOpts.RequestDeserializer(httpRequest, _tRequest, serializerCtx, cancellation) ?? _dtoInitializer());

        var req = (TRequest)_dtoInitializer();

        _fromBodyProp.PropSetter(
            req,
            await Cfg.SerOpts.RequestDeserializer(httpRequest, _fromBodyProp.PropType, serializerCtx, cancellation));

        return req;
    }

    static async ValueTask<TRequest> BindPlainTextBody(HttpRequest request)
    {
        var req = (IPlainTextRequest)_dtoInitializer();
        var reader = new StreamReader(request.Body);
        req.Content = await reader.ReadToEndAsync();
        request.HttpContext.Response.RegisterForDispose(reader); //disposing the reader immediately causes the request body to also get disposed.
        if (request.Body.CanSeek)                                //EnableBuffering() is used, so rewind. form binding fails if not rewound.
            request.Body.Seek(0, SeekOrigin.Begin);

        return (TRequest)req;
    }

    static void BindFormValues(TRequest req, HttpRequest httpRequest, List<ValidationFailure> failures, bool dontAutoBindForm)
    {
        if (!httpRequest.HasFormContentType || dontAutoBindForm)
            return;

        if (Cfg.BndOpts.FormExceptionTransformer is null)
            Execute();
        else
        {
            try
            {
                Execute();
            }
            catch (Exception e)
            {
                failures.Add(Cfg.BndOpts.FormExceptionTransformer(e));

                if (e is BadHttpRequestException { StatusCode: 413 }) //only short-circuit if it's a 413 payload size exceeded
                    throw new ValidationFailureException(failures, "Form binding failed!");
            }
        }

        void Execute()
        {
            if (_fromFormProp is null)
            {
                BindFormFields();
                BindFiles();
            }
            else
                ComplexFormBinder.Bind(_fromFormProp, req, httpRequest.Form);
        }

        void BindFormFields()
        {
            foreach (var kvp in httpRequest.Form)
                Bind(req, kvp, failures);
        }

        void BindFiles()
        {
            Dictionary<string, FormFileCollection>? formFileCollections =
                _formFileCollectionProps.Count > 0
                    ? new()
                    : null;

            for (var y = 0; y < httpRequest.Form.Files.Count; y++)
            {
                var formFile = httpRequest.Form.Files[y];
                var fieldName = formFile.BareFieldName();

                if (formFileCollections is not null && _formFileCollectionProps.ContainsKey(fieldName))
                {
                    if (formFileCollections.TryGetValue(fieldName, out var fileCollection))
                        fileCollection.Add(formFile);
                    else
                        formFileCollections[fieldName] = [formFile];

                    continue;
                }

                if (!_primaryProps.TryGetValue(formFile.Name, out var prop))
                    continue;

                if (prop.PropType == Types.IFormFile)
                    prop.PropSetter(req, formFile);
                else
                    failures.Add(new(formFile.Name, "Files can only be bound to properties of type IFormFile!"));
            }

            if (formFileCollections is not null)
            {
                foreach (var (key, value) in formFileCollections)
                {
                    if (_formFileCollectionProps.TryGetValue(key, out var prop))
                        prop.PropSetter(req, value);
                }
            }
        }
    }

    static void BindRouteValues(TRequest req, RouteValueDictionary routeValues, List<ValidationFailure> failures)
    {
        if (routeValues.Count == 0)
            return;

        foreach (var kvp in routeValues)
            Bind(req, new(kvp.Key, kvp.Value?.ToString()), failures);
    }

    static void BindQueryParams(TRequest req, IQueryCollection query, List<ValidationFailure> failures, JsonSerializerContext? serializerCtx)
    {
        if (query.Count == 0)
            return;

        foreach (var kvp in query)
            Bind(req, kvp, failures);

        if (_fromQueryParamsProp is not null)
        {
            var obj = new JsonObject(new() { PropertyNameCaseInsensitive = true });
            var sortedDic = new SortedDictionary<string, StringValues>(
                query.ToDictionary(x => x.Key, x => x.Value),
                StringComparer.OrdinalIgnoreCase);
            var swaggerStyle = !sortedDic.Any(x => x.Key.Contains('.') || x.Key.Contains("[0"));

            _fromQueryParamsProp.PropType.QueryObjectSetter()(sortedDic, obj, null, null, swaggerStyle);

            _fromQueryParamsProp.PropSetter(
                req,
                obj[Constants.QueryJsonNodeName].Deserialize(
                    _fromQueryParamsProp.PropType,
                    serializerCtx?.Options ?? Cfg.SerOpts.Options));
        }
    }

    static void BindUserClaims(TRequest req, IEnumerable<Claim> claims, List<ValidationFailure> failures)
    {
        for (var i = 0; i < _fromClaimProps.Count; i++)
        {
            var prop = _fromClaimProps[i];
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
        for (var i = 0; i < _fromHeaderProps.Count; i++)
        {
            var prop = _fromHeaderProps[i];
            var hdrVal = headers[prop.Identifier];

            switch (hdrVal.Count)
            {
                case 0 when prop.ForbidIfMissing:
                    failures.Add(new(prop.Identifier, "This header is missing from the request!"));

                    break;
                case > 0:
                {
                    var res = prop.ValueParser(hdrVal);
                    prop.PropSetter(req, res.Value);

                    if (!res.IsSuccess)
                        failures.Add(new(prop.Identifier, $"Unable to bind header value [{hdrVal}] to a [{prop.PropType.Name}] property!"));

                    break;
                }
            }
        }
    }

    static void BindHasPermissionProps(TRequest req, IEnumerable<Claim> claims, List<ValidationFailure> failures)
    {
        for (var i = 0; i < _hasPermissionProps.Count; i++)
        {
            var prop = _hasPermissionProps[i];
            var hasPerm = claims.Any(
                c => string.Equals(c.Type, Cfg.SecOpts.PermissionsClaimType, StringComparison.OrdinalIgnoreCase) &&
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
        if (_primaryProps.TryGetValue(kvp.Key, out var prop))
        {
            ParseResult res;

            try
            {
                res = prop.ValueParser(kvp.Value);
            }
            catch (JsonException ex)
            {
                throw new JsonBindException(kvp.Key, Cfg.BndOpts.FailureMessage(prop.PropType, kvp.Key, kvp.Value), ex);
            }

            if (res.IsSuccess || IsNullablePropAndInputIsEmptyString(kvp, prop))
                prop.PropSetter(req, res.Value);
            else
                failures.Add(new(kvp.Key, Cfg.BndOpts.FailureMessage(prop.PropType, kvp.Key, kvp.Value)));
        }

        static bool IsNullablePropAndInputIsEmptyString(KeyValuePair<string, StringValues> kvp, PrimaryPropCacheEntry prop)
            => kvp.Value[0]?.Length == 0 && Nullable.GetUnderlyingType(prop.PropType) is not null;
    }

    static bool AddFromClaimPropCacheEntry(FromClaimAttribute att, PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        _fromClaimProps.Add(
            new()
            {
                Identifier = att.ClaimType ?? propInfo.Name,
                ForbidIfMissing = att.IsRequired,
                PropType = propInfo.PropertyType,
                IsCollection = propInfo.PropertyType != Types.String && propInfo.PropertyType.GetInterfaces().Contains(Types.IEnumerable),
                ValueParser = propInfo.PropertyType.CachedValueParser(),
                PropSetter = compiledSetter
            });

        return !att.IsRequired; //if claim is optional, return true so it will also be added as a PropCacheEntry
    }

    static bool AddFromHeaderPropCacheEntry(FromHeaderAttribute att, PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        _fromHeaderProps.Add(
            new()
            {
                Identifier = att.HeaderName ?? propInfo.Name,
                ForbidIfMissing = att.IsRequired,
                PropType = propInfo.PropertyType,
                ValueParser = propInfo.PropertyType.CachedValueParser(),
                PropSetter = compiledSetter
            });

        return !att.IsRequired; //if header is optional, return true so it will also be added as a PropCacheEntry;
    }

    static bool AddHasPermissionPropCacheEntry(HasPermissionAttribute att, PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        _hasPermissionProps.Add(
            new()
            {
                Identifier = att.Permission,
                ForbidIfMissing = att.IsRequired,
                PropType = propInfo.PropertyType,
                PropName = propInfo.Name,
                ValueParser = propInfo.PropertyType.CachedValueParser(),
                PropSetter = compiledSetter
            });

        return false; // don't allow binding from any other sources
    }

    static void AddPrimaryPropCacheEntry(string? fieldName, PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        _primaryProps.Add(
            fieldName ?? propInfo.Name,
            new()
            {
                PropType = propInfo.PropertyType,
                ValueParser = propInfo.PropertyType.CachedValueParser(),
                PropSetter = compiledSetter
            });
    }

    static void AddFormFileCollectionPropCacheEntry(string? fieldName, PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        _formFileCollectionProps.Add(
            fieldName ?? propInfo.Name,
            new()
            {
                PropType = propInfo.PropertyType,
                PropSetter = compiledSetter
            });
    }

    static bool SetFromFormPropCache(PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        if (Types.IEnumerable.IsAssignableFrom(propInfo.PropertyType) || !propInfo.PropertyType.IsClass)
        {
            throw new InvalidOperationException(
                $"The property [{_tRequest.FullName}.{propInfo.Name}] has to be a complex type in order to " +
                "work with the [FromForm] attribute.");
        }

        _fromFormProp = new()
        {
            PropType = propInfo.PropertyType,
            PropSetter = compiledSetter
        };

        return false;
    }

    static bool SetFromBodyPropCache(PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        _fromBodyProp = new()
        {
            PropType = propInfo.PropertyType,
            PropSetter = compiledSetter
        };

        return false;
    }

    static bool SetFromQueryParamsPropCache(PropertyInfo propInfo, Action<object, object?> compiledSetter)
    {
        _fromQueryParamsProp = new()
        {
            PropType = propInfo.PropertyType,
            PropSetter = compiledSetter
        };

        return false;
    }
}