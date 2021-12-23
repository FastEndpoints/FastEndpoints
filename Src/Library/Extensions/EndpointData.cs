using FastEndpoints.Validation;
using System.Diagnostics;

namespace FastEndpoints;

internal sealed class EndpointData
{
    //using Lazy<T> to prevent contention when WAF testing (see issue #10)
    private readonly Lazy<EndpointDefinition[]> _endpoints = new(() =>
    {
        var epDefs = GenerateEndpointDefinitions();

        if (epDefs.Length == 0)
            throw new InvalidOperationException("FastEndpoints was unable to find any endpoint declarations!");

        return epDefs!;
    });

    internal EndpointDefinition[] Definitions => _endpoints.Value;

    internal static Stopwatch Stopwatch { get; } = new();

    private static EndpointDefinition[] GenerateEndpointDefinitions()
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
                        typeof(IEndpoint),
                        typeof(IValidator),
                        typeof(IEventHandler)
                }).Any());

        //Endpoint<TRequest>
        //Validator<TRequest>

        var epList = new List<(Type tEndpoint, Type tRequest)>();

        //key: TRequest //val: TValidator
        var valDict = new Dictionary<Type, Type>();

        foreach (var dscType in discoveredTypes)
        {
            foreach (var infType in dscType.GetInterfaces())
            {
                if (infType == typeof(IEventHandler))
                {
                    ((IEventHandler?)Activator.CreateInstance(dscType))?.Subscribe();
                }

                if (infType == typeof(IEndpoint))
                {
                    var tRequest = typeof(EmptyRequest);

                    if (dscType.BaseType?.IsGenericType is true)
                        tRequest = dscType.BaseType?.GetGenericArguments()?[0] ?? tRequest;

                    epList.Add((dscType, tRequest));
                }

                if (infType == typeof(IValidator))
                {
                    Type tRequest = dscType.BaseType?.GetGenericArguments()[0]!;
                    valDict.Add(tRequest, dscType);
                }
            }
        }

        return epList
            .Select(x =>
            {
                var instance = (IEndpoint)Activator.CreateInstance(x.tEndpoint)!;
                instance?.Configure();
                return new EndpointDefinition(
                    x.tEndpoint,
                    valDict.GetValueOrDefault(x.tRequest),
                    (EndpointSettings)BaseEndpoint.SettingsPropInfo.GetValue(instance)!);
            })
            .ToArray();
    }
}

internal record EndpointDefinition(
    Type EndpointType,
    Type? ValidatorType,
    EndpointSettings Settings);

