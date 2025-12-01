using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CA1822

namespace FastEndpoints;

/// <summary>
/// binder context supplied to custom request binders.
/// </summary>
public readonly struct BinderContext : IServiceResolverBase
{
    /// <summary>
    /// the http context of the current request
    /// </summary>
    public HttpContext HttpContext { get; init; }

    /// <summary>
    /// a list of validation failures for the endpoint. you can add your own validation failures for properties of the request dto using this property.
    /// </summary>
    public List<ValidationFailure> ValidationFailures { get; init; }

    /// <summary>
    /// the configured json serializer options of the app, which was specified at app startup.
    /// </summary>
    public JsonSerializerOptions SerializerOptions => Cfg.SerOpts.Options;

    /// <summary>
    /// if the current endpoint is configured with a json serializer context, it will be provided to the custom request binder with this property.
    /// </summary>
    public JsonSerializerContext? JsonSerializerContext { get; init; }

    /// <summary>
    /// set 'true' to disable auto binding of form data which enables uploading and reading of large files without buffering to memory/disk.
    /// you can access the multipart sections for reading via the FormFileSectionsAsync() method.
    /// </summary>
    public bool DontAutoBindForms { get; init; }

    readonly IEnumerable<string> _requiredProperties;
    internal List<string> BoundProperties { get; } = [];

    /// <summary>
    /// indicates which required properties were not bound due to missing input from the request.
    /// </summary>
    public IEnumerable<string> UnboundRequiredProperties => _requiredProperties.Except(BoundProperties, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// constructor of the binder context
    /// </summary>
    /// <param name="httpContext">the http context of the current request</param>
    /// <param name="validationFailures">the validation failure collection of the endpoint</param>
    /// <param name="jsonSerializerContext">json serializer context of the endpoint if applicable</param>
    /// <param name="dontAutoBindForms">whether to enable auto binding of form data</param>
    /// <param name="bindRequiredProps">collection of required property names</param>
    public BinderContext(HttpContext httpContext,
                         List<ValidationFailure> validationFailures,
                         JsonSerializerContext? jsonSerializerContext,
                         bool dontAutoBindForms,
                         IEnumerable<string> bindRequiredProps)
    {
        HttpContext = httpContext;
        ValidationFailures = validationFailures;
        JsonSerializerContext = jsonSerializerContext;
        DontAutoBindForms = dontAutoBindForms;
        _requiredProperties = bindRequiredProps;
    }

    /// <inheritdoc />
    public TService? TryResolve<TService>() where TService : class
        => ServiceResolver.Instance.TryResolve<TService>();

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService)
        => ServiceResolver.Instance.TryResolve(typeOfService);

    /// <inheritdoc />
    public TService Resolve<TService>() where TService : class
        => ServiceResolver.Instance.Resolve<TService>();

    /// <inheritdoc />
    public object Resolve(Type typeOfService)
        => ServiceResolver.Instance.Resolve(typeOfService);

    /// <inheritdoc />
    public IServiceScope CreateScope()
        => ServiceResolver.Instance.CreateScope();

    /// <inheritdoc />
    public TService? TryResolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.TryResolve<TService>(keyName);

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.TryResolve(typeOfService, keyName);

    /// <inheritdoc />
    public TService Resolve<TService>(string keyName) where TService : class
        => ServiceResolver.Instance.Resolve<TService>(keyName);

    /// <inheritdoc />
    public object Resolve(Type typeOfService, string keyName)
        => ServiceResolver.Instance.Resolve(typeOfService, keyName);
}