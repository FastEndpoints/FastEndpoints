using System.Diagnostics;
using System.Reflection;

namespace FastEndpoints;

//lives as a singleton in each DI container instance
sealed class EndpointData
{
    internal Stopwatch Stopwatch { get; } = new();

    internal EndpointDefinition[] Found { get; }

    internal EndpointData(EndpointDiscoveryOptions options, CommandHandlerRegistry cmdHandlerRegistry)
    {
        Stopwatch.Start();
        Found = BuildEndpointDefinitions(options, cmdHandlerRegistry);

        if (Found.Length == 0)
            throw new InvalidOperationException("FastEndpoints was unable to find any endpoint declarations!");
    }

    static EndpointDefinition[] BuildEndpointDefinitions(EndpointDiscoveryOptions options, CommandHandlerRegistry cmdHandlerRegistry)
    {
        if (options.DisableAutoDiscovery && options.Assemblies?.Any() is false)
            throw new InvalidOperationException("If 'DisableAutoDiscovery' is true, a collection of `Assemblies` must be provided!");

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

        var discoveredTypes = options.SourceGeneratorDiscoveredTypes.AsEnumerable();

        if (!discoveredTypes.Any())
        {
            var assemblies = Enumerable.Empty<Assembly>();

            if (options.Assemblies?.Any() is true)
                assemblies = options.Assemblies;

            if (!options.DisableAutoDiscovery)
                assemblies = assemblies.Union(AppDomain.CurrentDomain.GetAssemblies());

            if (options.AssemblyFilter is not null)
                assemblies = assemblies.Where(options.AssemblyFilter);

            discoveredTypes = assemblies
                              .Where(a => !a.IsDynamic && (options.Assemblies?.Contains(a) is true || !exclusions.Any(x => a.FullName!.StartsWith(x))))
                              .SelectMany(a => a.GetTypes())
                              .Where(
                                  t =>
                                      !t.IsDefined(Types.DontRegisterAttribute) &&
                                      t is { IsAbstract: false, IsInterface: false, IsGenericType: false } &&
                                      t.GetInterfaces().Intersect(
                                          new[]
                                          {
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
                    var tEndpoint = t.GetGenericArgumentsOfType(Types.SummaryOf1)?[0] ??
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
                        EventBase.HandlerDict[tEvent] = [t];

                    continue;
                }

                if (tGeneric == Types.ICommandHandlerOf1 || tGeneric == Types.ICommandHandlerOf2) // IsAssignableTo() is no good here also
                {
                    cmdHandlerRegistry.TryAdd(
                        key: tInterface.GetGenericArguments()[0],
                        value: new(t));

                    //continue;
                }
            }
        }

        return epList.Select(
            x =>
            {
                var def = new EndpointDefinition(x.tEndpoint, x.tRequest, x.tResponse);

                if (mapperDict.TryGetValue(x.tEndpoint, out var mapper))
                    def.MapperType = mapper;

                var implementsConfigure = false;
                var implementsHandleAsync = false;
                var implementsExecuteAsync = false;

                foreach (var m in x.tEndpoint.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy))
                {
                    switch (m.Name)
                    {
                        case nameof(BaseEndpoint.Configure) when !m.IsDefined(Types.NotImplementedAttribute, false):
                            implementsConfigure = true;

                            break;
                        case nameof(Endpoint<object>.HandleAsync) when !m.IsDefined(Types.NotImplementedAttribute, false):
                            implementsHandleAsync = true;

                            break;
                        case nameof(Endpoint<object>.ExecuteAsync) when !m.IsDefined(Types.NotImplementedAttribute, false):
                            implementsExecuteAsync = true;
                            def.ExecuteAsyncImplemented = true;

                            break;
                    }
                }

                def.ImplementsConfigure = implementsConfigure;
                def.EndpointAttributes = x.tEndpoint.GetCustomAttributes(true);
                var hasHttpAttrib = def.EndpointAttributes.Any(a => a is HttpAttribute);

                switch (implementsConfigure)
                {
                    case true when hasHttpAttrib:
                        throw new InvalidOperationException(
                            $"The endpoint [{x.tEndpoint.FullName}] has both Configure() method and attribute decorations on the class level. Only one of those strategies should be used!");
                    case false when !hasHttpAttrib:
                        throw new InvalidOperationException(
                            $"The endpoint [{x.tEndpoint.FullName}] should either override the Configure() method or decorate the class with a [Http*(...)] attribute!");
                }

                switch (implementsHandleAsync)
                {
                    case false when !implementsExecuteAsync:
                        throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] must implement either [HandleAsync] or [ExecuteAsync] methods!");
                    case true when implementsExecuteAsync:
                        throw new InvalidOperationException($"The endpoint [{x.tEndpoint.FullName}] has both [HandleAsync] and [ExecuteAsync] methods implemented!");
                }

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

    class ValDicItem
    {
        public readonly Type ValidatorType;
        public bool HasDuplicates;

        public ValDicItem(Type validatorType, bool dupesFound)
        {
            ValidatorType = validatorType;
            HasDuplicates = dupesFound;
        }
    }
}