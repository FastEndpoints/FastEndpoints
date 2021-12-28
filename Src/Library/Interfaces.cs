using FastEndpoints.Validation;
using FastEndpoints.Validation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints;

[HideFromDocs]
public interface IEndpoint
{
    HttpContext HttpContext { get; set; } //this is for writing extension methods by consumers
    List<ValidationFailure> ValidationFailures { get; } //also for extensibility
    void Configure();
}

/// <summary>
/// interface for defining pre-processors to be executed before the main endpoint handler is called
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public interface IPreProcessor<TRequest>
{
    Task PreProcessAsync(TRequest req, HttpContext ctx, List<ValidationFailure> failures, CancellationToken ct);
}

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
/// marker interface for entity mappers
/// </summary>
public interface IEntityMapper { }

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

internal interface IServiceResolver
{
    static IServiceProvider ServiceProvider { get; set; } //set only from .UseFastEndpoints() during startup

    static IHttpContextAccessor HttpContextAccessor => ServiceProvider.GetRequiredService<IHttpContextAccessor>();

    TService? TryResolve<TService>() where TService : class;
    object? TryResolve(Type typeOfService);

    TService Resolve<TService>() where TService : class;
    object Resolve(Type typeOfService);
}

internal interface IEventHandler
{
    void Subscribe();
}

internal interface IValidatorWithState : IValidator
{
    bool ThrowIfValidationFails { get; set; }
}


