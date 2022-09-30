using Microsoft.Extensions.Configuration;
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

    internal EndpointData(IServiceCollection services, EndpointDiscoveryOptions options, ConfigurationManager? config)
    {
        _endpoints = new(() =>
        {
            var endpoints = BuildEndpointDefinitions(services, options, config);

            return endpoints.Length == 0
                   ? throw new InvalidOperationException("FastEndpoints was unable to find any endpoint declarations!")
                   : endpoints;
        });

        //need this here to cause the lazy factory to run now.
        //because the endpoints are being added to DI container within the factory
        _ = _endpoints.Value;
    }

    private static EndpointDefinition[] BuildEndpointDefinitions(IServiceCollection services, EndpointDiscoveryOptions options,
        ConfigurationManager? config)
    {
        if (options.DisableAutoDiscovery && options.Assemblies?.Any() is false)
            throw new InvalidOperationException("If 'DisableAutoDiscovery' is true, a collection of `Assemblies` must be provided!");

        Stopwatch.Start();

        //also update FastEndpoints.Generator.EndpointsDiscoveryGenerator class if updating these
        IEnumerable<string> excludes = new[]
        {
            "Microsoft",
            "System",
            "FastEndpoints",
            "testhost",
            "netstandard",
            "Newtonsoft",
            "mscorlib",
            "NuGet",
            "NSwag",
            "FluentValidation",
            "YamlDotNet",
            "Accessibility",
            "NJsonSchema",
            "Namotion"
        };

        var discoveredTypes = options.SourceGeneratorDiscoveredTypes ?? Enumerable.Empty<Type>();

        if (!discoveredTypes.Any())
        {
            var assemblies = Enumerable.Empty<Assembly>();

            if (options.Assemblies?.Any() is true)
            {
                assemblies = options.Assemblies;
                excludes = excludes.Except(options.Assemblies.Select(a => a.FullName?.Split('.')[0]!));
            }

            if (!options.DisableAutoDiscovery)
                assemblies = assemblies.Union(AppDomain.CurrentDomain.GetAssemblies());

            if (options.AssemblyFilter is not null)
                assemblies = assemblies.Where(options.AssemblyFilter);

            discoveredTypes = assemblies
                .Where(a =>
                    !a.IsDynamic &&
                    !excludes.Any(n => a.FullName!.StartsWith(n)))
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                    !t.IsAbstract &&
                    !t.IsInterface &&
                    !t.IsGenericType &&
                     t.GetInterfaces().Intersect(new[] {
                         Types.IEndpoint,
                         Types.IEventHandler,
                         Types.ISummary,
                         Types.IMapper,
                         options.IncludeAbstractValidators ? Types.IValidator : Types.IEndpointValidator
                     }).Any() &&
                    (options.Filter is null || options.Filter(t)));
        }

        //Endpoint<TRequest>
        //Validator<TRequest>

        var epList = new List<(Type tEndpoint, Type tRequest)>();

        //key: TRequest
        var valDict = new Dictionary<Type, ValDicItem>();

        //key: TEndpoint //val: TSummary
        var summaryDict = new Dictionary<Type, Type>();

        foreach (var t in discoveredTypes)
        {
            foreach (var tInterface in t.GetInterfaces())
            {
                if (tInterface == Types.IEndpoint)
                {
                    var tRequest = t.GetGenericArgumentsOfType(Types.EndpointOf2)?[0] ?? Types.EmptyRequest;

                    services.AddTransient(t);
                    epList.Add((t, tRequest));
                    continue;
                }

                if (tInterface == Types.IValidator)
                {
                    var tRequest = t.GetGenericArgumentsOfType(Types.ValidatorOf1)?[0]!;

                    if (valDict.TryGetValue(tRequest, out var val))
                        val.HasDuplicates = true;
                    else
                        valDict.Add(tRequest, new(t, false));

                    continue;
                }

                if (tInterface == Types.ISummary)
                {
                    var tEndpoint =
                        t.GetGenericArgumentsOfType(Types.SummaryOf1)?[0]! ??
                        t.GetGenericArgumentsOfType(Types.SummaryOf2)?[0]!;
                    summaryDict.Add(tEndpoint, t);
                    continue;
                }

                if (tInterface == Types.IMapper)
                {
                    services.AddSingleton(t);
                    continue;
                }

                if (tInterface == Types.IEventHandler)
                {
                    var tEvent = t.GetGenericArgumentsOfType(Types.FastEventHandlerOf1)?[0]!;
                    var handler = (IEventHandler)Activator.CreateInstance(t)!;

                    if (EventBase.handlerDict.TryGetValue(tEvent, out var handlers))
                        handlers.Add(handler);
                    else
                        EventBase.handlerDict[tEvent] = new() { handler };

                    continue;
                }
            }
        }

        return epList.Select(x =>
        {
            var def = new EndpointDefinition()
            {
                EndpointType = x.tEndpoint,
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

            var implementsConfigure = false;
            var implementsHandleAsync = false;
            var implementsExecuteAsync = false;

            foreach (var m in x.tEndpoint.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
            {
                if (m.Name == nameof(BaseEndpoint.Configure) && !m.IsDefined(Types.NotImplementedAttribute, false))
                    implementsConfigure = true;

                if (m.Name == nameof(Endpoint<object>.HandleAsync) && !m.IsDefined(Types.NotImplementedAttribute, false))
                    implementsHandleAsync = true;

                if (m.Name == nameof(Endpoint<object>.ExecuteAsync) && !m.IsDefined(Types.NotImplementedAttribute, false))
                {
                    implementsExecuteAsync = true;
                    def.ExecuteAsyncImplemented = true;
                }
            }

            var epAttribs = x.tEndpoint.GetCustomAttributes(true);
            var hashttpAttrib = epAttribs.Any(a => a is HttpAttribute);

            if (implementsConfigure && hashttpAttrib)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] has both Configure() method and attribute decorations on the class level. Only one of those strategies should be used!");

            if (!implementsConfigure && !hashttpAttrib)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] should either override the Configure() method or decorate the class with a [Http*(...)] attribute!");

            if (!implementsHandleAsync && !implementsExecuteAsync)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] must implement either [HandleAsync] or [ExecuteAsync] methods!");

            if (implementsHandleAsync && implementsExecuteAsync)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] has both [HandleAsync] and [ExecuteAsync] methods implemented!");

            //create an endpoint instance and run the Configure() method in order to get the def object populated
            var instance =
                x.tEndpoint.GetConstructor(Type.EmptyTypes) is null
                ? (BaseEndpoint)FormatterServices.GetUninitializedObject(x.tEndpoint)! //this is an endpoint with ctor arguments
                : (BaseEndpoint)Activator.CreateInstance(x.tEndpoint)!; //endpoint which has a default ctor

            instance.Definition = def;
            instance.Config = config;

            def.Configure(instance, implementsConfigure, epAttribs);

            if (def.ValidatorType is null && valDict.TryGetValue(def.ReqDtoType, out var val)) //user has not specified a validator type in the endpoint
            {
                if (val.HasDuplicates)
                    throw new InvalidOperationException($"More than one validator was found for the request dto [{def.ReqDtoType.FullName}]. Specify the exact validator to register using the `Validator()` method in endpoint configuration.");

                def.ValidatorType = val.ValidatorType;
            }

            if (def.ValidatorType is not null)
            {
                if (def.ValidatorIsScoped) //todo: remove ability to make validators scoped in favor of CreateScope() method
                    services.AddScoped(def.ValidatorType);
                else
                    services.AddSingleton(def.ValidatorType);
            }

            if (summaryDict.TryGetValue(def.EndpointType, out var tSummary))
            {
                def.Summary((EndpointSummary)Activator.CreateInstance(tSummary)!);
            }

            return def;
        }).ToArray();
    }

    private class ValDicItem
    {
        public Type ValidatorType;
        public bool HasDuplicates;

        public ValDicItem(Type validatorType, bool dupesFound)
        {
            ValidatorType = validatorType;
            HasDuplicates = dupesFound;
        }
    }
}