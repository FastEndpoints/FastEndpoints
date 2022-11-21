using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace FastEndpoints;

/// <summary>
/// the common interface implemented by all endpoints
/// </summary>
public interface IEndpoint
{
    /// <summary>
    /// the http context of the current request
    /// </summary>
    HttpContext HttpContext { get; } //this is for allowing consumers to write extension methods

    /// <summary>
    /// validation failures collection for the endpoint
    /// </summary>
    List<ValidationFailure> ValidationFailures { get; } //also for extensibility

    /// <summary>
    /// gets the endpoint definition which contains all the configuration info for the endpoint
    /// </summary>
    EndpointDefinition Definition { get; } //also for extensibility

    //key: the type of the endpoint
    private static ConcurrentDictionary<Type, string> TestURLCache { get; } = new();

    internal static void SetTestURL(Type endpointType, string url) => TestURLCache[endpointType] = url;

    //don't change to internal. this is unofficially exposed to public.
    public static string TestURLFor<TEndpoint>() => TestURLCache[typeof(TEndpoint)];
}
