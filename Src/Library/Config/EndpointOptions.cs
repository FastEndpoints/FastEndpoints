namespace FastEndpoints;

/// <summary>
/// endpoint registration options
/// </summary>
public sealed class EndpointOptions
{
    /// <summary>
    /// specify a function to customize the endpoint name/swagger operation id. generate an endpoint name however you wish and return a string
    /// from your function. all available info for name generation is supplied via the <see cref="EndpointNameGenerationContext" />.
    /// </summary>
    public Func<EndpointNameGenerationContext, string> NameGenerator { internal get; set; } = EndpointNameGenerator;

    static string EndpointNameGenerator(EndpointNameGenerationContext ctx)
    {
        ctx.TagPrefix = ctx is { PrefixNameWithFirstTag: true, TagPrefix: not null } ? $"{ctx.TagPrefix}_" : null;
        ctx.HttpVerb = ctx.HttpVerb != null ? ctx.HttpVerb[0] + ctx.HttpVerb[1..].ToLowerInvariant() : null;
        var ep = ctx.ShortEndpointNames ? ctx.EndpointType.Name : ctx.EndpointType.FullName!.Replace(".", string.Empty);

        return $"{ctx.TagPrefix}{ctx.HttpVerb}{ep}{ctx.RouteNumber}";
    }

    /// <summary>
    /// set to true if you'd like the endpoint names/ swagger operation ids to be just the endpoint class names instead of the full names including
    /// namespace.
    /// </summary>
    public bool ShortNames { internal get; set; }

    /// <summary>
    /// set to true if you'd like to automatically prefix endpoint name (swagger operation id) with the first endpoint tag.
    /// the generated the operation id would be in the form of: <c>MyTag_CreateOrderEndpoint</c>.  this should come in handy with generating separate api clients
    /// with nswag using the "MultipleClientsFromOperationId" setting  which requires operation ids to be have a group name prefix with an underscore.
    /// </summary>
    public bool PrefixNameWithFirstTag { internal get; set; }

    /// <summary>
    /// prefix for all routes (example 'api').
    /// </summary>
    public string? RoutePrefix { internal get; set; }

    /// <summary>
    /// a function to filter out endpoints from auto registration.
    /// the function you set here will be executed for each endpoint during startup.
    /// you can inspect the EndpointSettings to check what the current endpoint is, if needed.
    /// return 'false' from the function if you want to exclude an endpoint from registration.
    /// return 'true' to include.
    /// this function will executed for each endpoint that has been discovered during startup.
    /// </summary>
    public Func<EndpointDefinition, bool>? Filter { internal get; set; }

    /// <summary>
    /// a configuration action to be performed on each endpoint definition during startup.
    /// some of the same methods you use inside `Configure()` method are available to be called on the `EndpointDefinition` parameter.
    /// this can be used to apply a set of common configuration settings globally to all endpoints.
    /// i.e. apply globally applicable settings here and specify only the settings applicable to individual endpoints from within each endpoints'
    /// `Configure()` method.
    /// <code>
    /// app.UseFastEndpoints(c => c.Configurator = ep =>
    /// {
    ///     ep.AllowAnonymous();
    ///     ep.Description(b => b.Produces&lt;ErrorResponse&gt;(400));
    /// });
    /// </code>
    /// </summary>
    public Action<EndpointDefinition>? Configurator { internal get; set; }

    /// <summary>
    /// allows the use of empty request dtos
    /// </summary>
    public bool AllowEmptyRequestDtos { internal get; set; }
}

public struct EndpointNameGenerationContext(Type endpointType, string? httpVerb, int? routeNumber, string? tagPrefix)
{
    public Type EndpointType { get; internal init; } = endpointType;
    public string? HttpVerb { get; internal set; } = httpVerb;
    public int? RouteNumber { get; internal init; } = routeNumber;
    public string? TagPrefix { get; internal set; } = tagPrefix;
    public bool PrefixNameWithFirstTag => Cfg.EpOpts.PrefixNameWithFirstTag;
    public bool ShortEndpointNames => Cfg.EpOpts.ShortNames;
}