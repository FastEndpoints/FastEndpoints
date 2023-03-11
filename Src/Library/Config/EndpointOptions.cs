namespace FastEndpoints;

/// <summary>
/// endpoint registration options
/// </summary>
public sealed class EndpointOptions
{
    /// <summary>
    /// set to true if you'd like the endpoint names/ swagger operation ids to be just the endpoint class names instead of the full names including namespace.
    /// </summary>
    public bool ShortNames { internal get; set; }

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
    /// i.e. apply globally applicable settings here and specify only the settings applicable to individual endpoints from within each endpoints' `Configure()` method.
    /// <code>
    /// app.UseFastEndpoints(c => c.Configurator = ep =>
    /// {
    ///     ep.AllowAnonymous();
    ///     ep.Description(b => b.Produces&lt;ErrorResponse&gt;(400));
    /// });
    /// </code>
    /// </summary>
    public Action<EndpointDefinition>? Configurator { internal get; set; }
}