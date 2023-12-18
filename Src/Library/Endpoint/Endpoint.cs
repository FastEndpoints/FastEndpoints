using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;

namespace FastEndpoints;

/// <summary>
/// use this base class for defining endpoints that only use a request dto and don't use a response dto.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class Endpoint<TRequest> : Endpoint<TRequest, object?> where TRequest : notnull { }

/// <summary>
/// use this base class for defining endpoints that only use a request dto and don't use a response dto but uses a request mapper.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TMapper">the type of the entity mapper</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class EndpointWithMapper<TRequest, TMapper> : Endpoint<TRequest, object?>, IHasMapper<TMapper>
    where TRequest : notnull
    where TMapper : IRequestMapper
{
    TMapper? _mapper;

    /// <summary>
    /// the entity mapper for the endpoint
    /// <para>HINT: entity mappers are singletons for performance reasons. do not maintain state in the mappers.</para>
    /// </summary>
    [DontInject]
    public TMapper Map //access is public to support testing
    {
        get => _mapper ??= (TMapper)Definition.GetMapper()!;
        set => _mapper = value; //allow unit tests to set mapper from outside
    }
}

/// <summary>
/// use this base class for defining endpoints that use both request and response dtos.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint, IEventBus, IServiceResolverBase where TRequest : notnull
{
    internal override async Task ExecAsync(CancellationToken ct)
    {
        TRequest req = default!;
        var ranPreProcessors = false;
        ExceptionDispatchInfo? edi = null;

        try
        {
            req = await BindRequestAsync(Definition, HttpContext, ValidationFailures, ct);

            //execution stops here if JsonException is thrown and continues at try/catch below

            OnBeforeValidate(req);
            await OnBeforeValidateAsync(req, ct);

            //execution stops here if ValidationFailureException is thrown and continues at try/catch below
            await ValidateRequest(req, Definition, ValidationFailures, ct);

            OnAfterValidate(req);
            await OnAfterValidateAsync(req, ct);

            await RunPreprocessors(Definition.PreProcessorList, req, HttpContext, ValidationFailures, ct);
            ranPreProcessors = true;

            if (ResponseStarted) //HttpContext.Response.HasStarted doesn't work in AWS lambda!!!
                return;          //response already sent to client (most likely from a preprocessor)

            OnBeforeHandle(req);
            await OnBeforeHandleAsync(req, ct);

            if (Definition.ExecuteAsyncImplemented)
            {
                _response = await ExecuteAsync(req, ct);

                if (Definition.ExecuteAsyncReturnsIResult)
                    await SendResultAsync((IResult)_response!);
            }
            else
                await HandleAsync(req, ct);

            if (!ResponseStarted)
                await AutoSendResponse(HttpContext, _response, Definition.SerializerContext, ct);

            OnAfterHandle(req, _response);
            await OnAfterHandleAsync(req, _response, ct);
        }
        catch (JsonException x) when (Cfg.BndOpts.JsonExceptionTransformer is not null)
        {
            if (ResponseStarted) //STJ already started response. it should be a showstopper at this point.
                throw;

            ValidationFailures.Add(Cfg.BndOpts.JsonExceptionTransformer(x));
            await ValidationFailed(x);
        }
        catch (ValidationFailureException x)
        {
            await ValidationFailed(x, x.StatusCode);
        }
        catch (Exception x)
        {
            edi = ExceptionDispatchInfo.Capture(x); //capturing the exception to allow post-procs to handle "uncaught" exceptions
        }
        finally
        {
            await RunPostProcessors(Definition.PostProcessorList, req, _response, HttpContext, edi, ValidationFailures, ct);

            //if a post-proc hasn't handled the captured exception and already written to the response, throw the captured exception.
            //without this UseDefaultExceptionHandler() or user's custom exception handling middleware becomes useless as the exception is silently swallowed.
            if (edi is not null && !ResponseStarted)
                edi.Throw();
        }

        async Task ValidationFailed(Exception x, int? statusCode = null)
        {
            if (!ranPreProcessors) //avoid running pre-procs twice
                await RunPreprocessors(Definition.PreProcessorList, req, HttpContext, ValidationFailures, ct);

            OnValidationFailed();
            await OnValidationFailedAsync(ct);

            if (Definition.DoNotCatchExceptions)
                throw x;

            if (!ResponseStarted) //pre-processors may have already sent a response
                await SendErrorsAsync(statusCode ?? Cfg.ErrOpts.StatusCode, ct);
        }
    }

    /// <summary>
    /// the handler method for the endpoint. this method is called for each request received.
    /// </summary>
    /// <param name="req">the request dto</param>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task HandleAsync(TRequest req, CancellationToken ct)
        => throw new NotImplementedException();

    /// <summary>
    /// the handler method for the endpoint that returns the response dto. this method is called for each request received.
    /// </summary>
    /// <param name="req">the request dto</param>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task<TResponse> ExecuteAsync(TRequest req, CancellationToken ct)
        => throw new NotImplementedException();

    /// <inheritdoc />
    public TService? TryResolve<TService>() where TService : class
        => Cfg.ServiceResolver.TryResolve<TService>();

    /// <inheritdoc />
    public object? TryResolve(Type typeOfService)
        => Cfg.ServiceResolver.TryResolve(typeOfService);

    /// <inheritdoc />
    public TService Resolve<TService>() where TService : class
        => Cfg.ServiceResolver.Resolve<TService>();

    /// <inheritdoc />
    public object Resolve(Type typeOfService)
        => Cfg.ServiceResolver.Resolve(typeOfService);

    /// <inheritdoc />
    public IServiceScope CreateScope()
        => Cfg.ServiceResolver.CreateScope();

    /// <summary>
    /// get the value of a given route parameter by specifying the resulting type and param name.
    /// NOTE: an automatic validation error is sent to the client when value retrieval is not successful.
    /// </summary>
    /// <typeparam name="T">type of the result</typeparam>
    /// <param name="paramName">route parameter name</param>
    /// <param name="isRequired">set to false for disabling the automatic validation error</param>
    /// <returns>the value if retrieval is successful or null if <paramref name="isRequired" /> is set to false</returns>
    protected T? Route<T>(string paramName, bool isRequired = true)
    {
        if (HttpContext.Request.RouteValues.TryGetValue(paramName, out var val))
        {
            var res = typeof(T).ValueParser()(val);

            if (res.IsSuccess)
                return (T?)res.Value;

            if (isRequired)
                ValidationFailures.Add(new(paramName, "Unable to read value of route parameter!"));
        }
        else if (isRequired)
            ValidationFailures.Add(new(paramName, "Route parameter was not found!"));

        ThrowIfAnyErrors();

        return default; // not required and retrieval failed
    }

    /// <summary>
    /// get the value of a given query parameter by specifying the resulting type and query parameter name.
    /// NOTE: an automatic validation error is sent to the client when value retrieval is not successful.
    /// </summary>
    /// <typeparam name="T">type of the result</typeparam>
    /// <param name="paramName">query parameter name</param>
    /// <param name="isRequired">set to false for disabling the automatic validation error</param>
    /// <returns>the value if retrieval is successful or null if <paramref name="isRequired" /> is set to false</returns>
    protected T? Query<T>(string paramName, bool isRequired = true)
    {
        if (HttpContext.Request.Query.TryGetValue(paramName, out var val))
        {
            var res = typeof(T).ValueParser()(val);

            if (res.IsSuccess)
                return (T?)res.Value;

            if (isRequired)
                ValidationFailures.Add(new(paramName, "Unable to read value of query parameter!"));
        }
        else if (isRequired)
            ValidationFailures.Add(new(paramName, "Query parameter was not found!"));

        ThrowIfAnyErrors();

        return default; // not required and retrieval failed
    }

    /// <summary>
    /// gets a stream of nullable FileMultipartSections from the incoming multipart/form-data without buffering the whole file to memory/disk
    /// as done with IFormFile
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    protected async IAsyncEnumerable<FileMultipartSection?> FormFileSectionsAsync([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var reader = new MultipartReader(HttpContext.Request.GetMultipartBoundary(), HttpContext.Request.Body);

        while (await reader.ReadNextSectionAsync(cancellation) is { } section)
        {
            if (section.GetContentDispositionHeader()?.IsFileDisposition() is true)
                yield return section.AsFileSection();
        }
    }

    /// <inheritdoc />
    public Task PublishAsync<TEvent>(TEvent eventModel, Mode waitMode = Mode.WaitForAll, CancellationToken cancellation = default)
        where TEvent : notnull
        => Cfg.ServiceResolver.Resolve<EventBus<TEvent>>().PublishAsync(eventModel, waitMode, cancellation);

    /// <summary>
    /// create the access/refresh token pair response with a given refresh-token service.
    /// </summary>
    /// <typeparam name="TService">the type of the token service</typeparam>
    /// <param name="userId">the id of the user for which the tokens will be generated for</param>
    /// <param name="userPrivileges">the user priviledges to be embeded in the jwt such as roles/claims/permissions</param>
    protected Task<TResponse> CreateTokenWith<TService>(string userId, Action<UserPrivileges> userPrivileges)
        where TService : IRefreshTokenService<TResponse>
        => ((IRefreshTokenService<TResponse>)
               Cfg.ServiceResolver.CreateInstance(typeof(TService), HttpContext.RequestServices)).CreateToken(userId, userPrivileges, null);

    /// <summary>
    /// retrieve the common processor state for this endpoint.
    /// </summary>
    /// <typeparam name="TState">the type of the processor state</typeparam>
    /// <exception cref="InvalidOperationException">
    /// thrown if the requested type of the processor state does not match with what's already stored in the
    /// context
    /// </exception>

    //access is public to support testing
    public TState ProcessorState<TState>() where TState : class, new()
        => HttpContext.ProcessorState<TState>();
}

/// <summary>
/// use this base class for defining endpoints that use both request and response dtos as well as require mapping to and from a domain entity
/// using a seperate entity mapper.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <typeparam name="TMapper">the type of the entity mapper</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class Endpoint<TRequest, TResponse, TMapper> : Endpoint<TRequest, TResponse>, IHasMapper<TMapper>
    where TRequest : notnull
    where TResponse : notnull
    where TMapper : IMapper
{
    TMapper? _mapper;

    /// <summary>
    /// the entity mapper for the endpoint
    /// <para>HINT: entity mappers are singletons for performance reasons. do not maintain state in the mappers.</para>
    /// </summary>
    [DontInject]

    //access is public to support testing
    public TMapper Map
    {
        get => _mapper ??= (TMapper)Definition.GetMapper()!;
        set => _mapper = value; //allow unit tests to set mapper from outside
    }

    /// <summary>
    /// send a response by mapping the supplied entity using this endpoint's mapper's SYNC mapping method.
    /// </summary>
    /// <typeparam name="TEntity">the type of the entity supplied</typeparam>
    /// <param name="entity">the entity instance to map to the response</param>
    /// <param name="statusCode">the status code to send</param>
    /// <param name="ct">optional cancellation token</param>
    protected Task SendMapped<TEntity>(TEntity entity, int statusCode = 200, CancellationToken ct = default)
    {
        var resp = ((Mapper<TRequest, TResponse, TEntity>)Definition.GetMapper()!).FromEntity(entity);

        return SendAsync(resp, statusCode, ct);
    }

    /// <summary>
    /// send a response by mapping the supplied entity using this endpoint's mapper's ASYNC mapping method.
    /// </summary>
    /// <typeparam name="TEntity">the type of the entity supplied</typeparam>
    /// <param name="entity">the entity instance to map to the response</param>
    /// <param name="statusCode">the status code to send</param>
    /// <param name="ct">optional cancellation token</param>
    protected async Task SendMappedAsync<TEntity>(TEntity entity, int statusCode = 200, CancellationToken ct = default)
    {
        var resp = await ((Mapper<TRequest, TResponse, TEntity>)Definition.GetMapper()!).FromEntityAsync(entity, ct);
        await SendAsync(resp, statusCode, ct);
    }
}

/// <summary>
/// use this base class for defining endpoints that doesn't need a request dto. usually used for routes that doesn't have any parameters.
/// </summary>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class EndpointWithoutRequest : Endpoint<EmptyRequest, object?>
{
    /// <summary>
    /// the handler method for the endpoint. this method is called for each request received.
    /// </summary>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task HandleAsync(CancellationToken ct)
        => throw new NotImplementedException();

    /// <summary>
    /// override the HandleAsync(CancellationToken ct) method instead of using this method!
    /// </summary>
    [NotImplemented]
    public sealed override Task HandleAsync(EmptyRequest _, CancellationToken ct)
        => HandleAsync(ct);

    /// <summary>
    /// the handler method for the endpoint. this method is called for each request received.
    /// </summary>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task<object?> ExecuteAsync(CancellationToken ct)
        => throw new NotImplementedException();

    /// <summary>
    /// override the ExecuteAsync(CancellationToken ct) method instead of using this method!
    /// </summary>
    [NotImplemented]
    public sealed override Task<object?> ExecuteAsync(EmptyRequest _, CancellationToken ct)
        => ExecuteAsync(ct);
}

/// <summary>
/// use this base class for defining endpoints that doesn't need a request dto but return a response dto.
/// </summary>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class EndpointWithoutRequest<TResponse> : Endpoint<EmptyRequest, TResponse>
{
    /// <summary>
    /// the handler method for the endpoint. this method is called for each request received.
    /// </summary>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task HandleAsync(CancellationToken ct)
        => throw new NotImplementedException();

    /// <summary>
    /// override the HandleAsync(CancellationToken ct) method instead of using this method!
    /// </summary>
    [NotImplemented]
    public sealed override Task HandleAsync(EmptyRequest _, CancellationToken ct)
        => HandleAsync(ct);

    /// <summary>
    /// the handler method for the endpoint that returns the response dto. this method is called for each request received.
    /// </summary>
    /// <param name="ct">a cancellation token</param>
    [NotImplemented]
    public virtual Task<TResponse> ExecuteAsync(CancellationToken ct)
        => throw new NotImplementedException();

    /// <summary>
    /// override the ExecuteAsync(CancellationToken ct) method instead of using this method!
    /// </summary>
    [NotImplemented]
    public sealed override Task<TResponse> ExecuteAsync(EmptyRequest _, CancellationToken ct)
        => ExecuteAsync(ct);
}

/// <summary>
/// use this base class for defining endpoints that doesn't need a request dto but return a response dto and uses a response mapper.
/// </summary>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <typeparam name="TMapper">the type of the entity mapper</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class EndpointWithoutRequest<TResponse, TMapper> : EndpointWithoutRequest<TResponse>, IHasMapper<TMapper>
    where TResponse : notnull
    where TMapper : IResponseMapper
{
    TMapper? _mapper;

    /// <summary>
    /// the entity mapper for the endpoint
    /// <para>HINT: entity mappers are singletons for performance reasons. do not maintain state in the mappers.</para>
    /// </summary>
    [DontInject]
    public TMapper Map //access is public to support testing
    {
        get => _mapper ??= (TMapper)Definition.GetMapper()!;
        set => _mapper = value; //allow unit tests to set mapper from outside
    }

    /// <summary>
    /// send a response by mapping the supplied entity using this endpoint's mapper's SYNC mapping method.
    /// </summary>
    /// <typeparam name="TEntity">the type of the entity supplied</typeparam>
    /// <param name="entity">the entity instance to map to the response</param>
    /// <param name="statusCode">the status code to send</param>
    /// <param name="ct">optional cancellation token</param>
    protected Task SendMapped<TEntity>(TEntity entity, int statusCode = 200, CancellationToken ct = default)
    {
        var resp = ((ResponseMapper<TResponse, TEntity>)Definition.GetMapper()!).FromEntity(entity);

        return SendAsync(resp, statusCode, ct);
    }

    /// <summary>
    /// send a response by mapping the supplied entity using this endpoint's mapper's ASYNC mapping method.
    /// </summary>
    /// <typeparam name="TEntity">the type of the entity supplied</typeparam>
    /// <param name="entity">the entity instance to map to the response</param>
    /// <param name="statusCode">the status code to send</param>
    /// <param name="ct">optional cancellation token</param>
    protected async Task SendMappedAsync<TEntity>(TEntity entity, int statusCode = 200, CancellationToken ct = default)
    {
        var resp = await ((ResponseMapper<TResponse, TEntity>)Definition.GetMapper()!).FromEntityAsync(entity, ct);
        await SendAsync(resp, statusCode, ct);
    }
}

/// <summary>
/// use this base class for defining endpoints that use both request and response dtos as well as require mapping to and from a domain entity.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
/// <typeparam name="TEntity">the type of domain entity that will be mapped to/from</typeparam>
[UsedImplicitly(ImplicitUseTargetFlags.WithInheritors)]
public abstract class EndpointWithMapping<TRequest, TResponse, TEntity> : Endpoint<TRequest, TResponse> where TRequest : notnull
{
    /// <summary>
    /// override this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto</param>
    public virtual TEntity MapToEntity(TRequest r)
        => throw new NotImplementedException($"Please override the {nameof(MapToEntity)} method!");

    /// <summary>
    /// override this method and place the logic for mapping the request dto to the desired domain entity
    /// </summary>
    /// <param name="r">the request dto to map from</param>
    /// <param name="ct">a cancellation token</param>
    public virtual Task<TEntity> MapToEntityAsync(TRequest r, CancellationToken ct = default)
        => throw new NotImplementedException($"Please override the {nameof(MapToEntityAsync)} method!");

    /// <summary>
    /// override this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    public virtual TResponse MapFromEntity(TEntity e)
        => throw new NotImplementedException($"Please override the {nameof(MapFromEntity)} method!");

    /// <summary>
    /// override this method and place the logic for mapping a domain entity to a response dto
    /// </summary>
    /// <param name="e">the domain entity to map from</param>
    /// <param name="ct">a cancellation token</param>
    public virtual Task<TResponse> MapFromEntityAsync(TEntity e, CancellationToken ct = default)
        => throw new NotImplementedException($"Please override the {nameof(MapFromEntityAsync)} method!");
}