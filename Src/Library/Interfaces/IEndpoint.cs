using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Collections.Concurrent;

namespace FastEndpoints;

[HideFromDocs]
public interface IEndpoint
{
    HttpContext HttpContext { get; } //this is for allowing consumers to write extension methods

    List<ValidationFailure> ValidationFailures { get; } //also for extensibility

    //key: the type of the endpoint
    private static ConcurrentDictionary<Type, string> TestURLCache { get; } = new();

    internal static void SetTestURL(Type endpointType, string url) => TestURLCache[endpointType] = url;

    public static string TestURLFor<TEndpoint>() => TestURLCache[typeof(TEndpoint)];
}
