using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Reflection;

namespace FastEndpoints;

internal sealed class EndpointData
{
    //using Lazy<T> to prevent contention when WAF testing (see issue #10)
    private Lazy<EndpointDefinition[]> _endpoints;

    internal EndpointDefinition[] Found => _endpoints.Value;

    internal static Stopwatch Stopwatch { get; } = new();

    internal EndpointData(EndpointDiscoveryOptions options, CommandHandlerRegistry cmdHandlerRegistry)
    {
        _endpoints = new(() =>
        {
            var endpoints = BuildEndpointDefinitions(options, cmdHandlerRegistry);
            return endpoints.Length == 0
                   ? throw new InvalidOperationException("FastEndpoints was unable to find any endpoint declarations!")
                   : endpoints;
        });
    }

    internal void Clear() => _endpoints = null!;

    private static EndpointDefinition[] BuildEndpointDefinitions(EndpointDiscoveryOptions options, CommandHandlerRegistry cmdHandlerRegistry)
    {
        if (options.DisableAutoDiscovery && options.Assemblies?.Any() is false)
            throw new InvalidOperationException("If 'DisableAutoDiscovery' is true, a collection of `Assemblies` must be provided!");

        Stopwatch.Start();

        IEnumerable<string> exclusions = new[]
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
            "Namotion",
            "StackExchange",
            "Grpc",
            "PresentationFramework",
            "PresentationCore",
            "WindowsBase"
        };

        var discoveredTypes = options.SourceGeneratorDiscoveredTypes ?? Enumerable.Empty<Type>();

        if (!discoveredTypes.Any())
        {
            var assemblies = Enumerable.Empty<Assembly>();

            if (options.Assemblies?.Any() is true)
            {
                //remove user supplied assemblies from exclusion list
                assemblies = options.Assemblies;
                exclusions = exclusions.Except(options.Assemblies.Select(a => a.FullName?.Split('.')[0]!));
            }

            if (!options.DisableAutoDiscovery)
                assemblies = assemblies.Union(AppDomain.CurrentDomain.GetAssemblies());

            if (options.AssemblyFilter is not null)
                assemblies = assemblies.Where(options.AssemblyFilter);

            discoveredTypes = assemblies
                .Where(a =>
                      !a.IsDynamic &&
                      !exclusions.Any(n => a.FullName!.StartsWith(n)))
                .SelectMany(a => a.GetTypes())
                .Where(t =>
                      !t.IsDefined(Types.DontRegisterAttribute) &&
                      !t.IsAbstract &&
                      !t.IsInterface &&
                      !t.IsGenericType &&
                       t.GetInterfaces().Intersect(new[] {
                           Types.IEndpoint,
                           Types.IEventHandler,
                           Types.ICommandHandler,
                           Types.ISummary,
                           options.IncludeAbstractValidators ? Types.IValidator : Types.IEndpointValidator
                       }).Any() &&
                       (options.Filter is null || options.Filter(t)));
        }

        //Endpoint<TRequest>
        //Validator<TRequest>

        var epList = new List<(Type tEndpoint, Type tRequest, Type tResponse)>();

        //key: TRequest
        var valDict = new Dictionary<Type, ValDicItem>();

        //key: TEndpoint //val: TMapper
        var mapperDict = new Dictionary<Type, Type>();

        //key: TEndpoint //val: TSummary
        var summaryDict = new Dictionary<Type, Type>();

        foreach (var t in discoveredTypes)
        {
            var tInterfaces = t.GetInterfaces();

            foreach (var tInterface in tInterfaces)
            {
                if (tInterface == Types.IEndpoint)
                {
                    var tRequest = t.GetGenericArgumentsOfType(Types.EndpointOf2)?[0] ?? Types.EmptyRequest;
                    var tResponse = t.GetGenericArgumentsOfType(Types.EndpointOf2)?[1] ?? Types.EmptyResponse;
                    epList.Add((t, tRequest, tResponse));

                    if (tInterfaces.Contains(Types.IHasMapper))
                    {
                        var tMapper =
                            t.GetGenericArgumentsOfType(Types.EndpointOf3)?[2] ??
                            t.GetGenericArgumentsOfType(Types.EndpointWithMapperOf2)?[1] ??
                            t.GetGenericArgumentsOfType(Types.EndpointWithOutRequestOf2)?[1];
                        if (Types.IMapper.IsAssignableFrom(tMapper))
                            mapperDict[t] = tMapper;
                    }
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

                var tGeneric = tInterface.IsGenericType ? tInterface.GetGenericTypeDefinition() : null;

                if (tGeneric == Types.IEventHandlerOf1) // IsAssignableTo() is no good here if the user inherits the interface.
                {
                    var tEvent = tInterface.GetGenericArguments()[0];

                    if (EventBase.HandlerDict.TryGetValue(tEvent, out var handlers))
                        handlers.Add(t);
                    else
                        EventBase.HandlerDict[tEvent] = new() { t };

                    continue;
                }

                if (tGeneric == Types.ICommandHandlerOf1 || tGeneric == Types.ICommandHandlerOf2) // IsAssignableTo() is no good here also
                {
                    cmdHandlerRegistry.TryAdd(
                        key: tInterface.GetGenericArguments()[0],
                        value: new CommandHandlerDefinition(t));

                    //continue;
                }
            }
        }

        return epList.Select(x =>
        {
            var def = new EndpointDefinition(x.tEndpoint, x.tRequest, x.tResponse);

            if (mapperDict.TryGetValue(x.tEndpoint, out var mapper))
                def.MapperType = mapper;

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

            def.ImplementsConfigure = implementsConfigure;
            def.EpAttributes = x.tEndpoint.GetCustomAttributes(true);
            var hashttpAttrib = def.EpAttributes.Any(a => a is HttpAttribute);

            if (implementsConfigure && hashttpAttrib)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] has both Configure() method and attribute decorations on the class level. Only one of those strategies should be used!");

            if (!implementsConfigure && !hashttpAttrib)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] should either override the Configure() method or decorate the class with a [Http*(...)] attribute!");

            if (!implementsHandleAsync && !implementsExecuteAsync)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] must implement either [HandleAsync] or [ExecuteAsync] methods!");

            if (implementsHandleAsync && implementsExecuteAsync)
                throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] has both [HandleAsync] and [ExecuteAsync] methods implemented!");

            if (valDict.TryGetValue(def.ReqDtoType, out var val))
            {
                if (val.HasDuplicates)
                    def.FoundDuplicateValidators = true;
                else
                    def.ValidatorType = val.ValidatorType;
            }

            if (summaryDict.TryGetValue(def.EndpointType, out var tSummary))
                def.Summary((EndpointSummary)Activator.CreateInstance(tSummary)!);

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