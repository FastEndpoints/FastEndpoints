using System.Collections.Concurrent;

// ReSharper disable UnusedMemberInSuper.Global

namespace FastEndpoints;

/// <summary>
/// the common interface implemented by all endpoints
/// </summary>
public interface IEndpoint : IResponseSender
{
    /// <summary>
    /// retrieves the name of a given endpoint by supplying its type. the name is generated using the <see cref="EndpointOptions.NameGenerator" /> func.
    /// </summary>
    /// <param name="verb">the http verb, if the target is a multi-verb endpoint.</param>
    /// <param name="routeNumber">the route number, if the target is a multi route endpoint.</param>
    /// <param name="tagPrefix">tag prefix</param>
    public static string GetName<TEndpoint>(Http? verb = null, int? routeNumber = null, string? tagPrefix = null) where TEndpoint : IEndpoint
        => Cfg.EpOpts.NameGenerator(new(typeof(TEndpoint), verb?.ToString("F"), routeNumber, tagPrefix));

    //key: the type of the endpoint
    private static ConcurrentDictionary<Type, string>? _testUrlCache;
    private static ConcurrentDictionary<Type, string> TestUrlCache => _testUrlCache ??= new(); //lazy init to prevent alloc at startup

    internal static void SetTestUrl(Type endpointType, string url)
        => TestUrlCache[endpointType] = url;

    //don't change to internal. this is unofficially exposed to public.
    public static string TestURLFor<TEndpoint>()
        => TestUrlCache[typeof(TEndpoint)];

    internal static IEnumerable<string> GetTestUrlCache()
    {
        foreach (var kvp in TestUrlCache)
            yield return $"{kvp.Key.FullName}|{kvp.Value}";
    }
}

/// <summary>
/// marker interface for endpoint base classes without a request dto
/// </summary>
public interface INoRequest;