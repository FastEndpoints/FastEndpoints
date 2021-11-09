using FastEndpoints.Validation;
using FastEndpoints.Validation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.Json;

namespace FastEndpoints;

[HideFromDocs]
public abstract class BaseEndpoint : IEndpoint
{
    internal static JsonSerializerOptions? SerializerOptions { get; set; } //set on app startup from .UseFastEndpoints()

#pragma warning disable CS8601,CS8602
    internal static PropertyInfo SettingsPropInfo { get; set; } = typeof(BaseEndpoint).GetProperty(nameof(Settings), BindingFlags.NonPublic | BindingFlags.Instance);
#pragma warning restore CS8601,CS8602

    internal EndpointSettings Settings { get; set; } = new();

    internal abstract Task ExecAsync(HttpContext ctx, IValidator validator, object preProcessors, object postProcessors, CancellationToken ct);

    internal string GetTestURL()
    {
        Configure();

        if (Settings.Routes is null)
            throw new ArgumentNullException($"GetTestURL()[{nameof(Settings.Routes)}]");

        return Settings.Routes[0];
    }

    /// <summary>
    /// the http context of the current request
    /// </summary>
#pragma warning disable CS8618
    public HttpContext HttpContext { get; set; }
#pragma warning restore CS8618

    /// <summary>
    /// use this method to configure how this endpoint should be listening to incoming requests
    /// </summary>
    public abstract void Configure();

    /// <summary>
    /// the list of validation failures for the current request dto
    /// </summary>
    public List<ValidationFailure> ValidationFailures { get; } = new();
}

/// <summary>
/// use this base class for defining endpoints that doesn't need a request dto. usually used for routes that doesn't have any parameters.
/// </summary>
public abstract class EndpointWithoutRequest : Endpoint<EmptyRequest> { }

/// <summary>
/// use this base class for defining endpoints that doesn't need a request dto but return a response dto.
/// </summary>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
public abstract class EndpointWithoutRequest<TResponse> : Endpoint<EmptyRequest, TResponse> where TResponse : notnull, new() { }

/// <summary>
/// use this base class for defining endpoints that only use a request dto and don't use a response dto.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public abstract class Endpoint<TRequest> : Endpoint<TRequest, object> where TRequest : notnull, new() { };

/// <summary>
/// use this base class for defining endpoints that use both request and response dtos.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new() where TResponse : notnull, new()
{
    /// <summary>
    /// override this method if you'd like to do something to the request dto before it gets validated.
    /// </summary>
    /// <param name="req">the request dto</param>
    protected virtual void OnBeforeValidate(TRequest req) { }
    /// <summary>
    /// override this method if you'd like to do something to the request dto after it gets validated.
    /// </summary>
    /// <param name="req">the request dto</param>
    protected virtual void OnAfterValidate(TRequest req) { }
    /// <summary>
    /// override this method if you'd like to do something to the request dto before the handler is executed.
    /// </summary>
    /// <param name="req">the request dto</param>
    protected virtual void OnBeforeHandle(TRequest req) { }
    /// <summary>
    /// override this method if you'd like to do something after the handler is executed.
    /// </summary>
    /// <param name="req">the request dto</param>
    /// <param name="res">the response dto that was sent to the client</param>
    protected virtual void OnAfterHandle(TRequest req, TResponse res) { }
    /// <summary>
    /// the handler method for the endpoint. this method is called for each request received.
    /// </summary>
    /// <param name="req">the request dto</param>
    /// <param name="ct">a cancellation token</param>
    public abstract Task HandleAsync(TRequest req, CancellationToken ct);
    internal override async Task ExecAsync(HttpContext ctx, IValidator? validator, object? preProcessors, object? postProcessors, CancellationToken cancellation)
    {
        HttpContext = ctx;
        try
        {
            var req = await BindToModelAsync(ctx, ValidationFailures, cancellation).ConfigureAwait(false);
            OnBeforeValidate(req);
            await ValidateRequestAsync(req, (IValidator<TRequest>?)validator, ctx, preProcessors, ValidationFailures, cancellation).ConfigureAwait(false);
            OnAfterValidate(req);
            await RunPreprocessors(preProcessors, req, ctx, ValidationFailures, cancellation).ConfigureAwait(false);
            OnBeforeHandle(req);
            await HandleAsync(req, cancellation).ConfigureAwait(false);
            OnAfterHandle(req, Response);
            await RunPostProcessors(postProcessors, req, Response, ctx, ValidationFailures, cancellation).ConfigureAwait(false);
        }
        catch (ValidationFailureException)
        {
            await SendErrorsAsync(cancellation).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    protected TService? TryResolve<TService>() => HttpContext.RequestServices.GetService<TService>();
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    protected object? TryResolve(Type typeOfService) => HttpContext.RequestServices.GetService(typeOfService);
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    protected TService Resolve<TService>() where TService : notnull => HttpContext.RequestServices.GetRequiredService<TService>();
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    protected object Resolve(Type typeOfService) => HttpContext.RequestServices.GetRequiredService(typeOfService);

    /// <summary>
    /// publish the given model/dto to all the subscribers of the event notification
    /// </summary>
    /// <param name="eventModel">the notification event model/dto to publish</param>
    /// <param name="waitMode">specify whether to wait for none, any or all of the subscribers to complete their work</param>
    ///<param name="cancellation">an optional cancellation token</param>
    /// <returns>a Task that matches the wait mode specified.
    /// Mode.WaitForNone returns an already completed Task (fire and forget).
    /// Mode.WaitForAny returns a Task that will complete when any of the subscribers complete their work.
    /// Mode.WaitForAll return a Task that will complete only when all of the subscribers complete their work.</returns>
    protected Task PublishAsync<TEvent>(TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default) where TEvent : class
        => Event<TEvent>.PublishAsync(eventModel, waitMode, cancellation);
}
