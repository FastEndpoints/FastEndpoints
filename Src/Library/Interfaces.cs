using FastEndpoints.Validation;
using FastEndpoints.Validation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

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

internal interface IEventHandler
{
    internal void Subscribe();
}

internal interface IValidatorWithState : IValidator
{
    internal bool ThrowIfValidationFails { get; set; }
    public IServiceProvider ServiceProvider { get; set; }
}

[HideFromDocs]
public interface IEndpoint
{
    HttpContext HttpContext { get; set; } //this is for writing extension methods by consumers
    List<ValidationFailure> ValidationFailures { get; } //also for extensibility
    void Configure();
}
