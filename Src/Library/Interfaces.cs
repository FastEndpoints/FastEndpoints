using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

[HideFromDocs]
public interface IEndpoint
{
    HttpContext HttpContext { get; } //this is for allowing consumers to write extension methods
    List<ValidationFailure> ValidationFailures { get; } //also for extensibility
    void Configure();

    //key: the type of the endpoint
    private static Dictionary<Type, string> TestURLCache { get; } = new();

    internal static void SetTestURL(Type endpointType, string url) => TestURLCache[endpointType] = url;
    public static string TestURLFor<TEndpoint>() => TestURLCache[typeof(TEndpoint)];
}

[HideFromDocs]
public interface ISummary { }

/// <summary>
/// interface for defining pre-processors to be executed before the main endpoint handler is called
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public interface IPreProcessor<TRequest>
{
    Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct);
}

/// <summary>
/// interface for defining global pre-processors to be executed before the main endpoint handler is called
/// </summary>
public interface IGlobalPreProcessor : IPreProcessor<object> { }

/// <summary>
/// interface for defining post-processors to be executed after the main endpoint handler is done
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
public interface IPostProcessor<TRequest, TResponse>
{
    Task PostProcessAsync(TRequest req, TResponse res, HttpContext ctx, IReadOnlyCollection<ValidationFailure> failures, CancellationToken ct);
}

/// <summary>
/// interface for defining global post-processors to be executed after the main endpoint handler is done
/// </summary>
public interface IGlobalPostProcessor : IPostProcessor<object, object> { }

/// <summary>
/// marker interface for entity mappers
/// </summary>
public interface IMapper { }

/// <summary>
/// marker interface for request only mappers
/// </summary>
public interface IRequestMapper : IMapper { }

/// <summary>
/// marker interface for response only mappers
/// </summary>
public interface IResponseMapper : IMapper { }

/// <summary>
/// marker/constraint for endpoints that have a mapper generic argument
/// </summary>
/// <typeparam name="TMapper"></typeparam>
public interface IHasMapper<TMapper> where TMapper : notnull, IMapper
{
    TMapper Map { get; }
}

/// <summary>
/// implement this interface on your request dto if you need to model bind the raw content body of an incoming http request
/// </summary>
public interface IPlainTextRequest
{
    /// <summary>
    /// the request body content will be bound to this property
    /// </summary>
    string Content { get; set; }
}

/// <summary>
/// create custom request binders by implementing this interface. by registering a custom modelbinder for an endpoint will completely disable the built-in model binding and completely depend on your implementation of the custom binder to return a correctly populated request dto for the endpoint.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public interface IRequestBinder<TRequest> where TRequest : notnull
{
    /// <summary>
    /// this method will be called by the library for binding the incoming request data and return a populated request dto object.
    /// access the incoming request data via the <c>RequestBinderContext</c> and populate a new request dto instance and return it from this method.
    /// </summary>
    /// <param name="ctx">request binder context encapsulating the incoming http request context, a list of validation failures for the endpoint, and an optional json serializer context.</param>
    /// <param name="ct">cancellation token</param>
    public ValueTask<TRequest> BindAsync(BinderContext ctx, CancellationToken ct);
}

///// <summary>
///// implement this interface on custom types you want to use with request dto model binding for route/query/form fields
///// </summary>
///// <typeparam name="TSelf"></typeparam>
//public interface IParseable<TSelf> where TSelf : notnull
//{
//    [RequiresPreviewFeatures]
//    static abstract bool TryParse(string? input, out TSelf? output);
//}

internal interface IServiceResolver
{
    static IServiceProvider RootServiceProvider { get; set; } //set only from .UseFastEndpoints() during startup

    static IHttpContextAccessor HttpContextAccessor => RootServiceProvider.GetRequiredService<IHttpContextAccessor>();

    TService? TryResolve<TService>() where TService : class;
    object? TryResolve(Type typeOfService);

    TService Resolve<TService>() where TService : class;
    object Resolve(Type typeOfService);
}

internal interface IEventHandler { }

internal interface IEventHandler<TEvent> : IEventHandler
{
    Task HandleAsync(TEvent eventModel, CancellationToken ct);
}

internal interface IEndpointValidator { }