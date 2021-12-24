using FastEndpoints.Validation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

/// <summary>
/// use this base class for defining endpoints that use both request and response dtos.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint, IServiceResolver where TRequest : notnull, new() where TResponse : notnull, new()
{
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

            OnBeforeValidate(req); await OnBeforeValidateAsync(req).ConfigureAwait(false);
            await ValidateRequest(req, (IValidator<TRequest>?)validator, ctx, preProcessors, ValidationFailures, cancellation).ConfigureAwait(false);
            OnAfterValidate(req); await OnAfterValidateAsync(req).ConfigureAwait(false);

            await RunPreprocessors(preProcessors, req, ctx, ValidationFailures, cancellation).ConfigureAwait(false);

            OnBeforeHandle(req); await OnBeforeHandleAsync(req).ConfigureAwait(false);
            await HandleAsync(req, cancellation).ConfigureAwait(false);
            OnAfterHandle(req, Response); await OnAfterHandleAsync(req, Response).ConfigureAwait(false);

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
    public TService? TryResolve<TService>() where TService : class => HttpContext.RequestServices.GetService<TService>();
    /// <summary>
    /// try to resolve an instance for the given type from the dependency injection container. will return null if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    public object? TryResolve(Type typeOfService) => HttpContext.RequestServices.GetService(typeOfService);
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <typeparam name="TService">the type of the service to resolve</typeparam>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public TService Resolve<TService>() where TService : class => HttpContext.RequestServices.GetRequiredService<TService>();
    /// <summary>
    /// resolve an instance for the given type from the dependency injection container. will throw if unresolvable.
    /// </summary>
    /// <param name="typeOfService">the type of the service to resolve</param>
    /// <exception cref="InvalidOperationException">Thrown if requested service cannot be resolved</exception>
    public object Resolve(Type typeOfService) => HttpContext.RequestServices.GetRequiredService(typeOfService);

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
    public Task PublishAsync<TEvent>(TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default) where TEvent : class
        => Event<TEvent>.PublishAsync(eventModel, waitMode, cancellation);
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
/// use this base class for defining endpoints that use both request and response dtos as well as require mapping to and from a domain entity.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <typeparam name="TEntity">the type of domain entity that will be mapped to/from</typeparam>
public abstract class Endpoint<TRequest, TResponse, TEntity> : Endpoint<TRequest, TResponse> where TRequest : notnull, new() where TResponse : notnull, new()
{
    /// <summary>
    /// override this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto</param>
    public virtual TEntity MapToEntity(TRequest r) => throw new NotImplementedException($"Please override the {nameof(MapToEntity)} method!");
    /// <summary>
    /// override this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto to map from</param>
    public virtual Task<TEntity> MapToEntityAsync(TRequest r) => throw new NotImplementedException($"Please override the {nameof(MapToEntityAsync)} method!");

    /// <summary>
    /// override this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    public virtual TResponse MapFromEntity(TEntity e) => throw new NotImplementedException($"Please override the {nameof(MapFromEntity)} method!");
    /// <summary>
    /// override this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    public virtual Task<TResponse> MapFromEntityAsync(TEntity e) => throw new NotImplementedException($"Please override the {nameof(MapFromEntityAsync)} method!");
}

/// <summary>
/// use this base class for defining endpoints that use both request and response dtos as well as an entity mapper.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <typeparam name="TEntityMapper">the type of the entity mapper</typeparam>
public abstract class EndpointWithMapper<TRequest, TResponse, TEntityMapper> : Endpoint<TRequest, TResponse> where TRequest : notnull, new() where TResponse : notnull, new() where TEntityMapper : IEntityMapper, new()
{
    /// <summary>
    /// The entity mapper for the endpoint
    /// </summary>
    public TEntityMapper Map { get; } = new();

    internal override Task ExecAsync(HttpContext ctx, IValidator? validator, object? preProcessors, object? postProcessors, CancellationToken cancellation)
    {
        Map.ServiceProvider = ctx.RequestServices;
        return base.ExecAsync(ctx, validator, preProcessors, postProcessors, cancellation);
    }
}