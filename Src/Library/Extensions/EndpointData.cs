using FastEndpoints.Validation;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.Serialization;

namespace FastEndpoints;

internal sealed class EndpointData
{
    //using Lazy<T> to prevent contention when WAF testing (see issue #10)
    private readonly Lazy<EndpointDefinition[]> _endpoints;

    internal EndpointDefinition[] Found => _endpoints.Value;

    internal static Stopwatch Stopwatch { get; } = new();

    internal EndpointData(IServiceCollection services)
    {
        _endpoints = new(() =>
        {
            var endpoints = BuildEndpointDefinitions(services);

            if (endpoints.Length == 0)
                throw new InvalidOperationException("FastEndpoints was unable to find any endpoint declarations!");

            return endpoints!;
        });

        //need this here to cause the lazy factory to run now.
        //cause the endpoints are being added to DI container within the factory
        _ = _endpoints.Value;
    }

    private static EndpointDefinition[] BuildEndpointDefinitions(IServiceCollection services)
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

        return epList.Select(x =>
        {
            var def = new EndpointDefinition()
            {
                EndpointType = x.tEndpoint,
                ValidatorType = valDict.GetValueOrDefault(x.tRequest),
                ReqDtoType = x.tRequest,
            };

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

            var implementsHandleAsync = false;
            var implementsExecuteAsync = false;

            foreach (var m in x.tEndpoint.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
            {
                if (m.Name == "HandleAsync" && !m.IsDefined(Types.NotImplementedAttribute, false))
                {
                    implementsHandleAsync = true;
                }

                if (m.Name == "ExecuteAsync" && !m.IsDefined(Types.NotImplementedAttribute, false))
                {
                    implementsExecuteAsync = true;
                }
            }

            if (!implementsHandleAsync && !implementsExecuteAsync)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] must implement either [HandleAsync] or [ExecuteAsync] methods!");

            if (implementsHandleAsync && implementsExecuteAsync)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] has both [HandleAsync] and [ExecuteAsync] methods implemented!");

            def.ExecuteAsyncImplemented = implementsExecuteAsync;

            var instance = (BaseEndpoint)FormatterServices.GetUninitializedObject(x.tEndpoint)!;
            instance.Configuration = def;
            instance.Configure();
            instance.AddTestURLToCache(x.tEndpoint);

            if (instance.Configuration.ValidatorType is not null)
            {
                var xxx = instance.Configuration.ValidatorType.FullName == "TestCases.OnBeforeAfterValidationTest.Validator";

                if (instance.Configuration.ScopedValidator)
                    services.AddScoped(instance.Configuration.ValidatorType);
                else
                    services.AddSingleton(instance.Configuration.ValidatorType);
            }

            return def;

        }).ToArray();
    }
}