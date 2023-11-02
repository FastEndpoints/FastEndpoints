using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// interface for defining post-processors to be executed after the main endpoint handler is done
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
/// <typeparam name="TResponse">the type of the response dto</typeparam>
public interface IPostProcessor<TRequest, TResponse>
{
    /// <summary>
    /// this method is called with the given arguments when the post-processor executes.
    /// </summary>
    /// <param name="context">the post-processor's context</param>
    Task PostProcessAsync(PostProcessorContext<TRequest, TResponse> context, CancellationToken ct);
}

/// <summary>
/// interface for defining global post-processors to be executed after the main endpoint handler is done
/// </summary>
public interface IGlobalPostProcessor : IPostProcessor<object, object?> { }