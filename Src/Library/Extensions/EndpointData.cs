using FastEndpoints.Validation;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;

namespace FastEndpoints;

internal sealed class EndpointData
{
    //using Lazy<T> to prevent contention when WAF testing (see issue #10)
    private readonly Lazy<IEnumerable<EndpointDefinition>> _endpoints;

    internal IEnumerable<EndpointDefinition> Found => _endpoints.Value;

    internal static Stopwatch Stopwatch { get; } = new();

    internal EndpointData(IServiceCollection services)
    {
        _endpoints = new(() =>
        {
            var endpoints = BuildEndpointDefinitions(services);

            if (!endpoints.Any())
                throw new InvalidOperationException("FastEndpoints was unable to find any endpoint declarations!");

            return endpoints!;
        });

        //need this here to cause the lazy factory to run now.
        //cause the endpoints are being added to DI container within the factory
        _ = _endpoints.Value;
    }

    private static IEnumerable<EndpointDefinition> BuildEndpointDefinitions(IServiceCollection services)
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

                    services.AddTransient(tDisc);
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

        var defBag = new ConcurrentBag<EndpointDefinition>();

        Parallel.ForEach(epList, new() { MaxDegreeOfParallelism = 4 }, x =>
        {
            var def = new EndpointDefinition()
            {
                EndpointType = x.tEndpoint,
                ValidatorType = valDict.GetValueOrDefault(x.tRequest),
                ReqDtoType = x.tRequest,
            };

            var validator = (IValidatorWithState?)(def.ValidatorType is null ? null : Activator.CreateInstance(def.ValidatorType));
            if (validator is not null)
            {
                validator.ThrowIfValidationFails = def.ThrowIfValidationFails;
                def.ValidatorInstance = validator;
            }

            var serviceBoundEpProps = def.EndpointType
                .GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(p => p.CanRead && p.CanWrite)
                .Select(p => new ServiceBoundEpProp()
                {
                    PropSetter = def.EndpointType.SetterForProp(p.Name),
                    PropType = p.PropertyType,
                })
                .ToArray();

            if (serviceBoundEpProps.Length > 0)
                def.ServiceBoundEpProps = serviceBoundEpProps;

            var instance = (BaseEndpoint)FormatterServices.GetUninitializedObject(x.tEndpoint)!;
            instance.Configuration = def;
            instance.Configure();
            instance.AddTestURLToCache(x.tEndpoint);

            defBag.Add(def);
        });

        return defBag;
    }
}