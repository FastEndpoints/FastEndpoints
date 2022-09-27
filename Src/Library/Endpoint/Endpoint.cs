using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using static FastEndpoints.Config;

namespace FastEndpoints;

/// <summary>
/// use this base class for defining endpoints that only use a request dto and don't use a response dto.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public abstract class Endpoint<TRequest> : Endpoint<TRequest, object> where TRequest : notnull, new() { };
/// <summary>
/// use this base class for defining endpoints that only use a request dto and don't use a response dto but uses a request mapper.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TMapper">the type of the entity mapper</typeparam>
public abstract class EndpointWithMapper<TRequest, TMapper> : Endpoint<TRequest, object>, IHasMapper<TMapper> where TRequest : notnull, new() where TMapper : notnull, IRequestMapper
{
    private TMapper? _mapper;

    ///// <summary>
    ///// the entity mapper for the endpoint
    ///// <para>HINT: entity mappers are singletons for performance reasons. do not maintain state in the mappers.</para>
    ///// </summary>
    public TMapper Map => _mapper ??= HttpContext.RequestServices.GetRequiredService<TMapper>();
}

/// <summary>
/// use this base class for defining endpoints that use both request and response dtos.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint, IServiceResolver where TRequest : notnull, new()
{
    internal async override Task ExecAsync(CancellationToken ct)
    {
        TRequest req = default!;

        try
        {
            var binder = (IRequestBinder<TRequest>)
                 (Definition.RequestBinder ??= HttpContext.RequestServices.GetRequiredService(typeof(IRequestBinder<TRequest>)));

            var binderCtx = new BinderContext(HttpContext, ValidationFailures, Definition.SerializerContext, Definition.DontBindFormData);
            req = await binder.BindAsync(binderCtx, ct);

            BndOpts.Modifier?.Invoke(req, tRequest, binderCtx, ct);

            OnBeforeValidate(req);
            await OnBeforeValidateAsync(req, ct);

            await ValidateRequest(
                req,
                HttpContext,
                Definition,
                Definition.PreProcessorList,
                ValidationFailures,
                ct);

            OnAfterValidate(req);
            await OnAfterValidateAsync(req, ct);

            await RunPreprocessors(Definition.PreProcessorList, req, HttpContext, ValidationFailures, ct);

            if (ResponseStarted) //HttpContext.Response.HasStarted doesn't work in AWS lambda!!!
                return; //response already sent to client (most likely from a preprocessor)

            OnBeforeHandle(req);
            await OnBeforeHandleAsync(req, ct);

            if (Definition.ExecuteAsyncImplemented)
                _response = await ExecuteAsync(req, ct);
            else
                await HandleAsync(req, ct);

            if (!ResponseStarted)
                await AutoSendResponse(HttpContext, _response, Definition.SerializerContext, ct);

            OnAfterHandle(req, Response);
            await OnAfterHandleAsync(req, Response, ct);
        }
        catch (ValidationFailureException)
        {
            OnValidationFailed();
            await OnValidationFailedAsync(ct);

            if (!Definition.DoNotCatchExceptions)
                await SendErrorsAsync(ErrOpts.StatusCode, ct);
            else
                throw;
        }
        finally
        {
            await RunPostProcessors(Definition.PostProcessorList, req, Response, HttpContext, ValidationFailures, ct);
        }
    }

    /// <summary>
    /// the handler method for the endpoint. this method is called for each request received.
    /// </summary>
    /// <param name="req">the request dto</param>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task HandleAsync(TRequest req, CancellationToken ct) => throw new NotImplementedException();

    /// <summary>
    /// the handler method for the endpoint that returns the response dto. this method is called for each request received.
    /// </summary>
    /// <param name="req">the request dto</param>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task<TResponse> ExecuteAsync(TRequest req, CancellationToken ct) => throw new NotImplementedException();

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
    /// if you'd like to resolve scoped or transient services from the DI container, obtain a service scope from this method and dispose the scope when the work is complete.
    ///<para>
    /// <code>
    /// using var scope = CreateScope();
    /// var scopedService = scope.ServiceProvider.GetService(...);
    /// </code>
    /// </para>
    /// </summary>
    public IServiceScope CreateScope() => HttpContext.RequestServices.CreateScope();

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
        => HttpContext.RequestServices.GetRequiredService<Event<TEvent>>().PublishAsync(eventModel, waitMode, cancellation);

    /// <summary>
    /// get the value of a given route parameter by specifying the resulting type and param name.
    /// NOTE: an automatic validation error is sent to the client when value retrieval is not successful.
    /// </summary>
    /// <typeparam name="T">type of the result</typeparam>
    /// <param name="paramName">route parameter name</param>
    /// <param name="isRequired">set to false for disabling the automatic validation error</param>
    /// <returns>the value if retrieval is successful or null if <paramref name="isRequired"/> is set to false</returns>
    protected T? Route<T>(string paramName, bool isRequired = true)
    {
        if (HttpContext.Request.RouteValues.TryGetValue(paramName, out var val))
        {
            var res = typeof(T).ValueParser()?.Invoke(val);

            if (res?.isSuccess is true)
                return (T?)res?.value;

            if (isRequired)
                ValidationFailures.Add(new(paramName, "Unable to read value of route parameter!"));
        }
        else if (isRequired)
        {
            ValidationFailures.Add(new(paramName, "Route parameter was not found!"));
        }

        ThrowIfAnyErrors();

        return default;// not required and retrieval failed
    }

    /// <summary>
    /// get the value of a given query parameter by specifying the resulting type and query parameter name.
    /// NOTE: an automatic validation error is sent to the client when value retrieval is not successful.
    /// </summary>
    /// <typeparam name="T">type of the result</typeparam>
    /// <param name="paramName">query parameter name</param>
    /// <param name="isRequired">set to false for disabling the automatic validation error</param>
    /// <returns>the value if retrieval is successful or null if <paramref name="isRequired"/> is set to false</returns>
    protected T? Query<T>(string paramName, bool isRequired = true)
    {
        if (HttpContext.Request.Query.TryGetValue(paramName, out var val))
        {
            var res = typeof(T).ValueParser()?.Invoke(val);

            if (res?.isSuccess is true)
                return (T?)res?.value;

            if (isRequired)
                ValidationFailures.Add(new(paramName, "Unable to read value of query parameter!"));
        }
        else if (isRequired)
        {
            ValidationFailures.Add(new(paramName, "Query parameter was not found!"));
        }

        ThrowIfAnyErrors();

        return default;// not required and retrieval failed
    }
}
/// <summary>
/// use this base class for defining endpoints that use both request and response dtos as well as require mapping to and from a domain entity using a seperate entity mapper.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <typeparam name="TMapper">the type of the entity mapper</typeparam>
public abstract class Endpoint<TRequest, TResponse, TMapper> : Endpoint<TRequest, TResponse>, IHasMapper<TMapper> where TRequest : notnull, new() where TMapper : notnull, IMapper
{
    private TMapper? _mapper;

    ///// <summary>
    ///// the entity mapper for the endpoint
    ///// <para>HINT: entity mappers are singletons for performance reasons. do not maintain state in the mappers.</para>
    ///// </summary>
    public TMapper Map => _mapper ??= HttpContext.RequestServices.GetRequiredService<TMapper>();
}

/// <summary>
/// use this base class for defining endpoints that doesn't need a request dto. usually used for routes that doesn't have any parameters.
/// </summary>
public abstract class EndpointWithoutRequest : Endpoint<EmptyRequest, object>
{
    /// <summary>
    /// the handler method for the endpoint. this method is called for each request received.
    /// </summary>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task HandleAsync(CancellationToken ct) => throw new NotImplementedException();

    /// <summary>
    /// override the HandleAsync(CancellationToken ct) method instead of using this method!
    /// </summary>
    [NotImplemented]
    public sealed override Task HandleAsync(EmptyRequest _, CancellationToken ct) => HandleAsync(ct);

    /// <summary>
    /// the handler method for the endpoint. this method is called for each request received.
    /// </summary>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task<object> ExecuteAsync(CancellationToken ct) => throw new NotImplementedException();

    /// <summary>
    /// override the ExecuteAsync(CancellationToken ct) method instead of using this method!
    /// </summary>
    [NotImplemented]
    public sealed override Task<object> ExecuteAsync(EmptyRequest _, CancellationToken ct) => ExecuteAsync(ct);
}

/// <summary>
/// use this base class for defining endpoints that doesn't need a request dto but return a response dto.
/// </summary>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
public abstract class EndpointWithoutRequest<TResponse> : Endpoint<EmptyRequest, TResponse>
{
    /// <summary>
    /// the handler method for the endpoint. this method is called for each request received.
    /// </summary>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task HandleAsync(CancellationToken ct) => throw new NotImplementedException();

    /// <summary>
    /// override the HandleAsync(CancellationToken ct) method instead of using this method!
    /// </summary>
    [NotImplemented]
    public sealed override Task HandleAsync(EmptyRequest _, CancellationToken ct) => HandleAsync(ct);

    /// <summary>
    /// the handler method for the endpoint that returns the response dto. this method is called for each request received.
    /// </summary>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task<TResponse> ExecuteAsync(CancellationToken ct) => throw new NotImplementedException();

    /// <summary>
    /// override the ExecuteAsync(CancellationToken ct) method instead of using this method!
    /// </summary>
    [NotImplemented]
    public sealed override Task<TResponse> ExecuteAsync(EmptyRequest _, CancellationToken ct) => ExecuteAsync(ct);
}
/// <summary>
/// use this base class for defining endpoints that doesn't need a request dto but return a response dto and uses a response mapper.
/// </summary>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <typeparam name="TMapper">the type of the entity mapper</typeparam>
public abstract class EndpointWithoutRequest<TResponse, TMapper> : EndpointWithoutRequest<TResponse>, IHasMapper<TMapper> where TResponse : notnull where TMapper : notnull, IResponseMapper
{
    private TMapper? _mapper;

    ///// <summary>
    ///// the entity mapper for the endpoint
    ///// <para>HINT: entity mappers are singletons for performance reasons. do not maintain state in the mappers.</para>
    ///// </summary>
    public TMapper Map => _mapper ??= HttpContext.RequestServices.GetRequiredService<TMapper>();
}

/// <summary>
/// use this base class for defining endpoints that use both request and response dtos as well as require mapping to and from a domain entity.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <typeparam name="TEntity">the type of domain entity that will be mapped to/from</typeparam>
public abstract class EndpointWithMapping<TRequest, TResponse, TEntity> : Endpoint<TRequest, TResponse> where TRequest : notnull, new()
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