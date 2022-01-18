using FastEndpoints.Validation;
using System.Diagnostics;

namespace FastEndpoints;

internal sealed class EndpointData
{
    //using Lazy<T> to prevent contention when WAF testing (see issue #10)
    private readonly Lazy<FoundEndpoint[]> _endpoints = new(() =>
    {
        var endpoints = FindEndpoints();

        if (endpoints.Length == 0)
            throw new InvalidOperationException("FastEndpoints was unable to find any endpoint declarations!");

        return endpoints!;
    });

    internal FoundEndpoint[] Found => _endpoints.Value;

    internal static Stopwatch Stopwatch { get; } = new();

    private static FoundEndpoint[] FindEndpoints()
    {
        Stopwatch.Start();

        var excludes = new[]
        {
                "Microsoft.",
                "System.",
                "FastEndpoints.",
                "testhost",
                "netstandard",
                "Newtonsoft.",
                "mscorlib",
                "NuGet."
        };

        var discoveredTypes = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(a =>
                !a.IsDynamic &&
                !excludes.Any(n => a.FullName!.StartsWith(n)))
            .SelectMany(a => a.GetTypes())
            .Where(t =>
                !t.IsAbstract &&
                !t.IsInterface &&
                t.GetInterfaces().Intersect(new[] {
                        Types.IEndpoint,
                        Types.IValidator,
                        Types.IEventHandler
                }).Any());

        //Endpoint<TRequest>
        //Validator<TRequest>

        var epList = new List<(Type tEndpoint, Type tRequest)>();

        //key: TRequest //val: TValidator
        var valDict = new Dictionary<Type, Type>();

        foreach (var tDisc in discoveredTypes)
        {
            foreach (var tInterface in tDisc.GetInterfaces())
            {
                if (tInterface == Types.IEndpoint)
                {
                    var tRequest = Types.EmptyRequest;

                    if (tDisc.BaseType?.IsGenericType is true)
                        tRequest = tDisc.BaseType?.GetGenericArguments()?[0] ?? tRequest;

                    epList.Add((tDisc, tRequest));
                    continue;
                }

                if (tInterface == Types.IValidator)
                {
                    Type tRequest = tDisc.BaseType?.GetGenericArguments()[0]!;
                    valDict.Add(tRequest, tDisc);
                    continue;
                }

                if (tInterface == Types.IEventHandler)
                {
                    ((IEventHandler?)Activator.CreateInstance(tDisc))?.Subscribe();
                    continue;
                }
            }
        }

        return epList
            .Select(x =>
            {
                var instance = (IEndpoint)Activator.CreateInstance(x.tEndpoint)!;
                instance?.Configure();
                return new FoundEndpoint(
                    x.tEndpoint,
                    valDict.GetValueOrDefault(x.tRequest),
                    (EndpointSettings)BaseEndpoint.SettingsPropInfo.GetValue(instance)!);
            })
            .ToArray();
    }
}

internal record FoundEndpoint(
    Type EndpointType,
    Type? ValidatorType,
    EndpointSettings Settings);