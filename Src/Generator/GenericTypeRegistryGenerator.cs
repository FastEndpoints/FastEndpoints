using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Immutable;
using System.Linq;
using System.Text;

namespace FastEndpoints.Generator;

/// <summary>
/// Generates pre-computed generic type instantiations for AOT compatibility.
/// This eliminates MakeGenericType calls at runtime by pre-registering all needed combinations.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class GenericTypeRegistryGenerator : IIncrementalGenerator
{
    #region Constants

    const string DontRegisterAttribute = "DontRegisterAttribute";
    const string ICommandHandlerOf1 = "FastEndpoints.ICommandHandler<TCommand>";
    const string ICommandHandlerOf2 = "FastEndpoints.ICommandHandler<TCommand, TResult>";
    const string IPreProcessorOf1 = "FastEndpoints.IPreProcessor<TRequest>";
    const string IPostProcessorOf2 = "FastEndpoints.IPostProcessor<TRequest, TResponse>";
    const string IEventHandlerOf1 = "FastEndpoints.IEventHandler<TEvent>";
    const string IEndpoint = "FastEndpoints.IEndpoint";
    const string Endpoint2 = "FastEndpoints.Endpoint<TRequest, TResponse>";
    const string IGlobalPreProcessor = "FastEndpoints.IGlobalPreProcessor";
    const string IGlobalPostProcessor = "FastEndpoints.IGlobalPostProcessor";
    const string IJobStorageProviderOf1 = "FastEndpoints.IJobStorageProvider<TStorageRecord>";

    #endregion

    #region Data Structures

    sealed class CommandHandlerInfo
    {
        public string CommandType { get; }
        public string? ResultType { get; }
        public string HandlerType { get; }

        public CommandHandlerInfo(string commandType, string? resultType, string handlerType)
        {
            CommandType = commandType;
            ResultType = resultType;
            HandlerType = handlerType;
        }

        public override bool Equals(object? obj)
            => obj is CommandHandlerInfo other && CommandType == other.CommandType && HandlerType == other.HandlerType;

        public override int GetHashCode()
            => (CommandType, HandlerType).GetHashCode();
    }

    sealed class EventHandlerInfo
    {
        public string EventType { get; }
        public string HandlerType { get; }

        public EventHandlerInfo(string eventType, string handlerType)
        {
            EventType = eventType;
            HandlerType = handlerType;
        }

        public override bool Equals(object? obj)
            => obj is EventHandlerInfo other && EventType == other.EventType;

        public override int GetHashCode()
            => EventType.GetHashCode();
    }

    sealed class ProcessorInfo
    {
        public string ProcessorType { get; }
        public string? RequestType { get; }
        public string? ResponseType { get; }
        public bool IsPreProcessor { get; }
        public bool IsOpenGeneric { get; }
        public int GenericArgCount { get; }

        public ProcessorInfo(string processorType, string? requestType, string? responseType, bool isPreProcessor, bool isOpenGeneric = false, int genericArgCount = 0)
        {
            ProcessorType = processorType;
            RequestType = requestType;
            ResponseType = responseType;
            IsPreProcessor = isPreProcessor;
            IsOpenGeneric = isOpenGeneric;
            GenericArgCount = genericArgCount;
        }

        public override bool Equals(object? obj)
            => obj is ProcessorInfo other && ProcessorType == other.ProcessorType;

        public override int GetHashCode()
            => ProcessorType.GetHashCode();
    }

    sealed class JobStorageProviderInfo
    {
        public string StorageRecordType { get; }
        public string StorageProviderType { get; }

        public JobStorageProviderInfo(string storageRecordType, string storageProviderType)
        {
            StorageRecordType = storageRecordType;
            StorageProviderType = storageProviderType;
        }

        public override bool Equals(object? obj)
            => obj is JobStorageProviderInfo other && StorageProviderType == other.StorageProviderType;

        public override int GetHashCode()
            => StorageProviderType.GetHashCode();
    }

    sealed class EndpointInfo
    {
        public string EndpointType { get; }
        public string RequestType { get; }
        public string ResponseType { get; }
        public List<string> PropertyTypes { get; }

        public EndpointInfo(string endpointType, string requestType, string responseType, List<string> propertyTypes)
        {
            EndpointType = endpointType;
            RequestType = requestType;
            ResponseType = responseType;
            PropertyTypes = propertyTypes;
        }

        public override bool Equals(object? obj)
            => obj is EndpointInfo other && EndpointType == other.EndpointType;

        public override int GetHashCode()
            => EndpointType.GetHashCode();
    }

    #endregion

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var assemblyNameProvider = context.CompilationProvider
            .Select(static (compilation, _) => compilation.AssemblyName ?? "Assembly");

        // Discover command handlers
        var commandHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(IsCandidate, ExtractCommandHandler)
            .Where(static x => x is not null)
            .Collect();

        // Discover event handlers
        var eventHandlers = context.SyntaxProvider
            .CreateSyntaxProvider(IsCandidate, ExtractEventHandler)
            .Where(static x => x is not null)
            .Collect();

        // Discover pre/post processors
        var processors = context.SyntaxProvider
            .CreateSyntaxProvider(IsCandidate, ExtractProcessor)
            .Where(static x => x is not null)
            .Collect();

        // Discover endpoints with request/response types
        var endpoints = context.SyntaxProvider
            .CreateSyntaxProvider(IsCandidate, ExtractEndpoint)
            .Where(static x => x is not null)
            .Collect();

        // Discover job storage providers
        var jobStorageProviders = context.SyntaxProvider
            .CreateSyntaxProvider(IsCandidate, ExtractJobStorageProvider)
            .Where(static x => x is not null)
            .Collect();

        // Combine all
        var combined = assemblyNameProvider
            .Combine(commandHandlers)
            .Combine(eventHandlers)
            .Combine(processors)
            .Combine(endpoints)
            .Combine(jobStorageProviders);

        context.RegisterSourceOutput(combined, GenerateSource!);
    }

    static bool IsCandidate(SyntaxNode node, CancellationToken _)
        => node is ClassDeclarationSyntax;

    static EventHandlerInfo? ExtractEventHandler(GeneratorSyntaxContext context, CancellationToken ct)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, ct) is not INamedTypeSymbol typeSymbol)
            return null;

        if (typeSymbol.IsAbstract || HasAttribute(typeSymbol, DontRegisterAttribute))
            return null;

        // Skip open generic event handlers - they can't be directly mapped
        if (typeSymbol.IsGenericType)
            return null;

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var ifaceStr = iface.OriginalDefinition.ToDisplayString();

            if (ifaceStr == IEventHandlerOf1 && iface.TypeArguments.Length == 1)
            {
                return new EventHandlerInfo(
                    iface.TypeArguments[0].ToDisplayString(),
                    typeSymbol.ToDisplayString());
            }
        }

        return null;
    }

    static JobStorageProviderInfo? ExtractJobStorageProvider(GeneratorSyntaxContext context, CancellationToken ct)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, ct) is not INamedTypeSymbol typeSymbol)
            return null;

        if (typeSymbol.IsAbstract || HasAttribute(typeSymbol, DontRegisterAttribute))
            return null;

        // Skip open generic storage providers - they can't be directly mapped
        if (typeSymbol.IsGenericType)
            return null;

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var ifaceStr = iface.OriginalDefinition.ToDisplayString();

            if (ifaceStr == IJobStorageProviderOf1 && iface.TypeArguments.Length == 1)
            {
                return new JobStorageProviderInfo(
                    iface.TypeArguments[0].ToDisplayString(),
                    typeSymbol.ToDisplayString());
            }
        }

        return null;
    }

    static CommandHandlerInfo? ExtractCommandHandler(GeneratorSyntaxContext context, CancellationToken ct)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, ct) is not INamedTypeSymbol typeSymbol)
            return null;

        if (typeSymbol.IsAbstract)
            return null;

        if (typeSymbol.AllInterfaces.Length == 0)
            return null;

        if (HasAttribute(typeSymbol, DontRegisterAttribute))
            return null;

        // Skip open generic handlers - they can't be directly mapped
        if (typeSymbol.IsGenericType)
            return null;

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var ifaceStr = iface.OriginalDefinition.ToDisplayString();

            if (ifaceStr == ICommandHandlerOf2 && iface.TypeArguments.Length == 2)
            {
                return new CommandHandlerInfo(
                    iface.TypeArguments[0].ToDisplayString(),
                    iface.TypeArguments[1].ToDisplayString(),
                    typeSymbol.ToDisplayString());
            }

            if (ifaceStr == ICommandHandlerOf1 && iface.TypeArguments.Length == 1)
            {
                return new CommandHandlerInfo(
                    iface.TypeArguments[0].ToDisplayString(),
                    null,
                    typeSymbol.ToDisplayString());
            }
        }

        return null;
    }

    static ProcessorInfo? ExtractProcessor(GeneratorSyntaxContext context, CancellationToken ct)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, ct) is not INamedTypeSymbol typeSymbol)
            return null;

        if (typeSymbol.IsAbstract || HasAttribute(typeSymbol, DontRegisterAttribute))
            return null;

        foreach (var iface in typeSymbol.AllInterfaces)
        {
            var ifaceStr = iface.OriginalDefinition.ToDisplayString();

            // Check for open generic pre-processor: MyProcessor<TReq> : IPreProcessor<TReq>
            // where the type argument is a type parameter of the processor itself
            if (ifaceStr == IPreProcessorOf1 && iface.TypeArguments.Length == 1)
            {
                var typeArg = iface.TypeArguments[0];
                if (typeArg.TypeKind == TypeKind.TypeParameter && typeSymbol.IsGenericType)
                {
                    // This is an open generic pre-processor
                    return new ProcessorInfo(
                        typeSymbol.ToDisplayString(),
                        null,
                        null,
                        true,
                        true,
                        typeSymbol.TypeParameters.Length);
                }

                // Closed generic pre-processor: IPreProcessor<TRequest>
                return new ProcessorInfo(
                    typeSymbol.ToDisplayString(),
                    typeArg.ToDisplayString(),
                    null,
                    true);
            }

            // Check for open generic post-processor: MyProcessor<TReq, TRes> : IPostProcessor<TReq, TRes>
            // where the type arguments are type parameters of the processor itself
            if (ifaceStr == IPostProcessorOf2 && iface.TypeArguments.Length == 2)
            {
                var reqArg = iface.TypeArguments[0];
                var resArg = iface.TypeArguments[1];
                if (reqArg.TypeKind == TypeKind.TypeParameter && resArg.TypeKind == TypeKind.TypeParameter && typeSymbol.IsGenericType)
                {
                    // This is an open generic post-processor
                    return new ProcessorInfo(
                        typeSymbol.ToDisplayString(),
                        null,
                        null,
                        false,
                        true,
                        typeSymbol.TypeParameters.Length);
                }

                // Closed generic post-processor: IPostProcessor<TRequest, TResponse>
                return new ProcessorInfo(
                    typeSymbol.ToDisplayString(),
                    reqArg.ToDisplayString(),
                    resArg.ToDisplayString(),
                    false);
            }

            // Check for open generic processors implementing IGlobalPreProcessor or IGlobalPostProcessor
            if (ifaceStr == IGlobalPreProcessor && typeSymbol.IsGenericType)
            {
                return new ProcessorInfo(
                    typeSymbol.ToDisplayString(),
                    null,
                    null,
                    true,
                    true,
                    typeSymbol.TypeParameters.Length);
            }

            if (ifaceStr == IGlobalPostProcessor && typeSymbol.IsGenericType)
            {
                return new ProcessorInfo(
                    typeSymbol.ToDisplayString(),
                    null,
                    null,
                    false,
                    true,
                    typeSymbol.TypeParameters.Length);
            }
        }

        return null;
    }

    static EndpointInfo? ExtractEndpoint(GeneratorSyntaxContext context, CancellationToken ct)
    {
        if (context.SemanticModel.GetDeclaredSymbol(context.Node, ct) is not INamedTypeSymbol typeSymbol)
            return null;

        if (typeSymbol.IsAbstract || HasAttribute(typeSymbol, DontRegisterAttribute))
            return null;

        // Check if implements IEndpoint
        if (!typeSymbol.AllInterfaces.Any(i => i.ToDisplayString() == IEndpoint))
            return null;

        // Find Endpoint<TRequest, TResponse> in base types
        var baseType = typeSymbol.BaseType;
        while (baseType is not null)
        {
            if (baseType.OriginalDefinition.ToDisplayString() == Endpoint2 && baseType.TypeArguments.Length == 2)
            {
                var requestType = baseType.TypeArguments[0];
                var responseType = baseType.TypeArguments[1];

                // Collect all property types from request/response for List<T> pre-generation
                var propertyTypes = new List<string>();
                CollectPropertyTypes(requestType, propertyTypes, new HashSet<string>());
                CollectPropertyTypes(responseType, propertyTypes, new HashSet<string>());

                return new EndpointInfo(
                    typeSymbol.ToDisplayString(),
                    GetNonNullableTypeName(requestType),
                    GetNonNullableTypeName(responseType),
                    propertyTypes);
            }
            baseType = baseType.BaseType;
        }

        return null;
    }

    static void CollectPropertyTypes(ITypeSymbol type, List<string> propertyTypes, HashSet<string> visited)
    {
        var typeName = type.ToDisplayString();
        if (visited.Contains(typeName))
            return;
        visited.Add(typeName);

        // Skip primitives and well-known types
        if (type.SpecialType != SpecialType.None || typeName.StartsWith("System."))
            return;

        foreach (var member in type.GetMembers())
        {
            if (member is not IPropertySymbol prop || prop.DeclaredAccessibility != Accessibility.Public)
                continue;

            var propType = prop.Type;
            var propTypeName = GetNonNullableTypeName(propType);

            // Check if it's a collection type (List<T>, IEnumerable<T>, etc.)
            if (propType is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                var genericDef = namedType.OriginalDefinition.ToDisplayString();
                if (genericDef.StartsWith("System.Collections.Generic.") && namedType.TypeArguments.Length == 1)
                {
                    var elementType = namedType.TypeArguments[0];
                    var elementTypeName = GetNonNullableTypeName(elementType);
                    if (!propertyTypes.Contains(elementTypeName))
                        propertyTypes.Add(elementTypeName);

                    // Recursively collect from element type
                    CollectPropertyTypes(elementType, propertyTypes, visited);
                }
            }

            // Recursively collect from complex property types
            if (propType.TypeKind == TypeKind.Class || propType.TypeKind == TypeKind.Struct)
            {
                CollectPropertyTypes(propType, propertyTypes, visited);
            }
        }
    }

    static string GetNonNullableTypeName(ITypeSymbol type)
    {
        // Simply strip any trailing '?' from the type name to handle nullable reference types
        var name = type.ToDisplayString();
        return name.EndsWith("?") ? name.Substring(0, name.Length - 1) : name;
    }

    static bool HasAttribute(INamedTypeSymbol typeSymbol, string attributeName)
    {
        foreach (var attribute in typeSymbol.GetAttributes())
        {
            if (string.Equals(attribute.AttributeClass?.Name, attributeName, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    static void GenerateSource(
        SourceProductionContext context,
        (((((string AssemblyName, ImmutableArray<CommandHandlerInfo?> Handlers), ImmutableArray<EventHandlerInfo?> Events), ImmutableArray<ProcessorInfo?> Processors), ImmutableArray<EndpointInfo?> Endpoints), ImmutableArray<JobStorageProviderInfo?> JobStorageProviders) data)
    {
        var assemblyName = data.Item1.Item1.Item1.Item1.AssemblyName;

        var handlers = data.Item1.Item1.Item1.Item1.Handlers
            .Where(x => x is not null)
            .Cast<CommandHandlerInfo>()
            .Distinct()
            .OrderBy(x => x.CommandType)
            .ToImmutableArray();

        var eventHandlers = data.Item1.Item1.Item1.Events
            .Where(x => x is not null)
            .Cast<EventHandlerInfo>()
            .Distinct()
            .OrderBy(x => x.EventType)
            .ToImmutableArray();

        var processors = data.Item1.Item1.Processors
            .Where(x => x is not null)
            .Cast<ProcessorInfo>()
            .Distinct()
            .OrderBy(x => x.ProcessorType)
            .ToImmutableArray();

        var endpoints = data.Item1.Endpoints
            .Where(x => x is not null)
            .Cast<EndpointInfo>()
            .Distinct()
            .OrderBy(x => x.EndpointType)
            .ToImmutableArray();

        var jobStorageProviders = data.JobStorageProviders
            .Where(x => x is not null)
            .Cast<JobStorageProviderInfo>()
            .Distinct()
            .OrderBy(x => x.StorageProviderType)
            .ToImmutableArray();

        if (handlers.Length == 0 && eventHandlers.Length == 0 && processors.Length == 0 && endpoints.Length == 0)
            return;

        var source = GenerateRegistryClass(assemblyName, handlers, eventHandlers, processors, endpoints, jobStorageProviders);
        context.AddSource("GenericTypeRegistry.g.cs", SourceText.From(source, Encoding.UTF8));
    }

    static string GenerateRegistryClass(
        string assemblyName,
        ImmutableArray<CommandHandlerInfo> handlers,
        ImmutableArray<EventHandlerInfo> eventHandlers,
        ImmutableArray<ProcessorInfo> processors,
        ImmutableArray<EndpointInfo> endpoints,
        ImmutableArray<JobStorageProviderInfo> jobStorageProviders)
    {
        var sb = new StringBuilder();

        // Collect all unique property element types for List<T> pre-generation
        var allPropertyTypes = endpoints
            .SelectMany(e => e.PropertyTypes)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        // Collect open generic processors
        var openPreProcessors = processors.Where(p => p.IsOpenGeneric && p.IsPreProcessor).ToList();
        var openPostProcessors = processors.Where(p => p.IsOpenGeneric && !p.IsPreProcessor).ToList();

        // Separate handlers with results from void handlers
        var handlersWithResult = handlers.Where(h => h.ResultType != null).ToList();
        var voidHandlers = handlers.Where(h => h.ResultType == null).ToList();

        // Collect unique event types from event handlers
        var uniqueEventTypes = eventHandlers
            .Select(e => e.EventType)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        sb.AppendLine($$"""
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by FastEndpoints.Generator.
            //     Provides AOT-compatible type lookups and DynamicDependency hints.
            // </auto-generated>
            //------------------------------------------------------------------------------

            #nullable enable
            #pragma warning disable CS0618

            namespace {{assemblyName}};

            using System;
            using System.Collections.Generic;
            using System.Diagnostics.CodeAnalysis;
            using System.Runtime.CompilerServices;

            /// <summary>
            /// Pre-computed type registry for AOT-compatible type resolution.
            /// </summary>
            public static class GenericTypeRegistry
            {
                /// <summary>
                /// Ensures AOT/trimming preserves all discovered command handlers.
                /// </summary>
            """);

        // Add DynamicDependency for each command handler
        foreach (var handler in handlers)
        {
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({handler.HandlerType}))]");
        }

        // Add DynamicDependency for each event handler
        foreach (var evtHandler in eventHandlers)
        {
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({evtHandler.HandlerType}))]");
        }

        // Add DynamicDependency for each processor
        foreach (var proc in processors)
        {
            var typeName = proc.IsOpenGeneric 
                ? ToUnboundedGenericSyntax(proc.ProcessorType, proc.GenericArgCount)
                : proc.ProcessorType;
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({typeName}))]");
        }

        // Add DynamicDependency for List<T> types
        foreach (var propType in allPropertyTypes)
        {
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(System.Collections.Generic.List<{propType}>))]");
        }

        // Add DynamicDependency for EventBus<T> types
        foreach (var eventType in uniqueEventTypes)
        {
            sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof(FastEndpoints.EventBus<{eventType}>))]");
        }

        // Note: JobQueue<,,,> is internal to FastEndpoints.JobQueues - cannot generate DynamicDependency
        // The runtime will use the AOT-safe factory registration instead.

        // Add DynamicDependency for all closed pre-processor types (processor + request type combinations)
        foreach (var openProc in openPreProcessors)
        {
            var baseTypeName = GetOpenGenericBaseTypeName(openProc.ProcessorType);
            foreach (var ep in endpoints)
            {
                sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({baseTypeName}<{ep.RequestType}>))]");
            }
        }

        // Add DynamicDependency for all closed post-processor types (processor + request/response type combinations)
        foreach (var openProc in openPostProcessors)
        {
            var baseTypeName = GetOpenGenericBaseTypeName(openProc.ProcessorType);
            foreach (var ep in endpoints)
            {
                sb.AppendLine($"    [DynamicDependency(DynamicallyAccessedMemberTypes.All, typeof({baseTypeName}<{ep.RequestType}, {ep.ResponseType}>))]");
            }
        }

        sb.AppendLine("""
                public static void EnsureAotPreservation() { }

                /// <summary>
                /// Factory functions for creating closed pre-processor instances.
                /// These direct new() calls ensure the AOT compiler preserves the constructors.
                /// </summary>
                public static readonly Dictionary<(Type, Type), Func<object>> ClosedPreProcessorFactories = new()
                {
            """);

        // Generate factory functions for all closed pre-processor types
        var preProcessorFactoryEntries = new HashSet<string>();
        foreach (var openProc in openPreProcessors)
        {
            var baseTypeName = GetOpenGenericBaseTypeName(openProc.ProcessorType);
            foreach (var ep in endpoints)
            {
                var key = $"[(typeof({ToUnboundedGenericSyntax(openProc.ProcessorType, openProc.GenericArgCount)}), typeof({ep.RequestType}))] = () => new {baseTypeName}<{ep.RequestType}>()";
                preProcessorFactoryEntries.Add(key);
            }
        }
        foreach (var entry in preProcessorFactoryEntries.OrderBy(x => x))
        {
            sb.AppendLine($"        {entry},");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Factory functions for creating closed post-processor instances.
                /// These direct new() calls ensure the AOT compiler preserves the constructors.
                /// </summary>
                public static readonly Dictionary<(Type, Type, Type), Func<object>> ClosedPostProcessorFactories = new()
                {
            """);

        // Generate factory functions for all closed post-processor types  
        var postProcessorFactoryEntries = new HashSet<string>();
        foreach (var openProc in openPostProcessors)
        {
            var baseTypeName = GetOpenGenericBaseTypeName(openProc.ProcessorType);
            foreach (var ep in endpoints)
            {
                var key = $"[(typeof({ToUnboundedGenericSyntax(openProc.ProcessorType, openProc.GenericArgCount)}), typeof({ep.RequestType}), typeof({ep.ResponseType}))] = () => new {baseTypeName}<{ep.RequestType}, {ep.ResponseType}>()";
                postProcessorFactoryEntries.Add(key);
            }
        }
        foreach (var entry in postProcessorFactoryEntries.OrderBy(x => x))
        {
            sb.AppendLine($"        {entry},");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps command types to their handler types.
                /// </summary>
                public static readonly Dictionary<Type, Type> CommandHandlers = new()
                {
            """);

        foreach (var handler in handlers)
        {
            sb.AppendLine($"        [typeof({handler.CommandType})] = typeof({handler.HandlerType}),");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps request types to their pre-processor types.
                /// </summary>
                public static readonly Dictionary<Type, List<Type>> PreProcessors = new()
                {
            """);

        // Group pre-processors by request type
        var preProcessorsByRequest = processors
            .Where(p => p.IsPreProcessor && !p.IsOpenGeneric && p.RequestType != null)
            .GroupBy(p => p.RequestType!)
            .ToDictionary(g => g.Key, g => g.Select(p => p.ProcessorType).ToList());

        foreach (var kvp in preProcessorsByRequest)
        {
            var types = string.Join(", ", kvp.Value.Select(t => $"typeof({t})"));
            sb.AppendLine($"        [typeof({kvp.Key})] = [{types}],");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps request types to their post-processor types.
                /// </summary>
                public static readonly Dictionary<Type, List<Type>> PostProcessors = new()
                {
            """);

        var postProcessorsByRequest = processors
            .Where(p => !p.IsPreProcessor && !p.IsOpenGeneric && p.RequestType != null)
            .GroupBy(p => p.RequestType!)
            .ToDictionary(g => g.Key, g => g.Select(p => p.ProcessorType).ToList());

        foreach (var kvp in postProcessorsByRequest)
        {
            var types = string.Join(", ", kvp.Value.Select(t => $"typeof({t})"));
            sb.AppendLine($"        [typeof({kvp.Key})] = [{types}],");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps (openGenericProcessor, requestType) to closed pre-processor types.
                /// Pre-computes all combinations of open generic pre-processors with all endpoint request types.
                /// </summary>
                public static readonly Dictionary<(Type, Type), Type> ClosedPreProcessors = new()
                {
            """);

        // Generate closed pre-processor types for all combinations of open generic pre-processors and request types
        foreach (var openProc in openPreProcessors)
        {
            // Get the base type name without generic parameters (e.g., "Namespace.Processor<TReq>" -> "Namespace.Processor")
            var baseTypeName = GetOpenGenericBaseTypeName(openProc.ProcessorType);
            var unboundedTypeName = ToUnboundedGenericSyntax(openProc.ProcessorType, openProc.GenericArgCount);
            foreach (var ep in endpoints)
            {
                sb.AppendLine($"        [(typeof({unboundedTypeName}), typeof({ep.RequestType}))] = typeof({baseTypeName}<{ep.RequestType}>),");
            }
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps (openGenericProcessor, requestType, responseType) to closed post-processor types.
                /// Pre-computes all combinations of open generic post-processors with all endpoint request/response types.
                /// </summary>
                public static readonly Dictionary<(Type, Type, Type), Type> ClosedPostProcessors = new()
                {
            """);

        // Generate closed post-processor types for all combinations of open generic post-processors and request/response types
        foreach (var openProc in openPostProcessors)
        {
            var baseTypeName = GetOpenGenericBaseTypeName(openProc.ProcessorType);
            var unboundedTypeName = ToUnboundedGenericSyntax(openProc.ProcessorType, openProc.GenericArgCount);
            foreach (var ep in endpoints)
            {
                sb.AppendLine($"        [(typeof({unboundedTypeName}), typeof({ep.RequestType}), typeof({ep.ResponseType}))] = typeof({baseTypeName}<{ep.RequestType}, {ep.ResponseType}>),");
            }
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps endpoint types to their request/response type pair.
                /// </summary>
                public static readonly Dictionary<Type, (Type RequestType, Type ResponseType)> EndpointTypes = new()
                {
            """);

        foreach (var ep in endpoints)
        {
            sb.AppendLine($"        [typeof({ep.EndpointType})] = (typeof({ep.RequestType}), typeof({ep.ResponseType})),");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps element types to their List&lt;T&gt; closed generic types.
                /// </summary>
                public static readonly Dictionary<Type, Type> ListTypes = new()
                {
            """);

        foreach (var propType in allPropertyTypes)
        {
            sb.AppendLine($"        [typeof({propType})] = typeof(System.Collections.Generic.List<{propType}>),");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Command and result type pairs for CommandHandlerExecutor registration.
                /// The actual CommandHandlerExecutor types are constructed internally by the provider.
                /// </summary>
                public static readonly List<(Type Command, Type Result)> CommandHandlerExecutorTypePairs = new()
                {
            """);

        foreach (var handler in handlersWithResult)
        {
            sb.AppendLine($"        (typeof({handler.CommandType}), typeof({handler.ResultType})),");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps command types to their ICommandHandler&lt;TCommand&gt; or ICommandHandler&lt;TCommand, TResult&gt; interface types.
                /// </summary>
                public static readonly Dictionary<Type, Type> CommandHandlerInterfaces = new()
                {
            """);

        foreach (var handler in voidHandlers)
        {
            sb.AppendLine($"        [typeof({handler.CommandType})] = typeof(FastEndpoints.ICommandHandler<{handler.CommandType}>),");
        }
        foreach (var handler in handlersWithResult)
        {
            sb.AppendLine($"        [typeof({handler.CommandType})] = typeof(FastEndpoints.ICommandHandler<{handler.CommandType}, {handler.ResultType}>),");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps event types to their EventBus&lt;TEvent&gt; types.
                /// </summary>
                public static readonly Dictionary<Type, Type> EventBusTypes = new()
                {
            """);

        foreach (var eventType in uniqueEventTypes)
        {
            sb.AppendLine($"        [typeof({eventType})] = typeof(FastEndpoints.EventBus<{eventType}>),");
        }

        sb.AppendLine("""
                };

                /// <summary>
                /// Maps event types to their event handler types.
                /// </summary>
                public static readonly Dictionary<Type, List<Type>> EventHandlers = new()
                {
            """);

        // Group event handlers by event type
        var eventHandlersByEvent = eventHandlers
            .GroupBy(e => e.EventType)
            .ToDictionary(g => g.Key, g => g.Select(e => e.HandlerType).ToList());

        foreach (var kvp in eventHandlersByEvent)
        {
            var types = string.Join(", ", kvp.Value.Select(t => $"typeof({t})"));
            sb.AppendLine($"        [typeof({kvp.Key})] = [{types}],");
        }

        sb.AppendLine("""
                };

                // Note: JobQueueTypes and JobQueueFactories are not generated here because
                // JobQueue<,,,> is internal to FastEndpoints.JobQueues assembly.
                // AOT support for JobQueues will be handled via a different mechanism.

                /// <summary>
                /// Attempts to get a command handler type for a command.
                /// </summary>
                public static bool TryGetCommandHandler(Type commandType, [NotNullWhen(true)] out Type? handlerType)
                    => CommandHandlers.TryGetValue(commandType, out handlerType);

                /// <summary>
                /// Attempts to get a List&lt;T&gt; type for an element type.
                /// </summary>
                public static bool TryGetListType(Type elementType, [NotNullWhen(true)] out Type? listType)
                    => ListTypes.TryGetValue(elementType, out listType);

                /// <summary>
                /// Registers this assembly's types with the central registry.
                /// Called automatically via module initializer.
                /// </summary>
                [ModuleInitializer]
                public static void Initialize()
                {
                    FastEndpoints.GenericTypeRegistryProvider.RegisterFromAssembly(
                        CommandHandlers,
                        PreProcessors,
                        PostProcessors,
                        EndpointTypes);
                    FastEndpoints.GenericTypeRegistryProvider.RegisterListTypes(ListTypes);
                    FastEndpoints.GenericTypeRegistryProvider.RegisterClosedPreProcessors(ClosedPreProcessors);
                    FastEndpoints.GenericTypeRegistryProvider.RegisterClosedPostProcessors(ClosedPostProcessors);
                    FastEndpoints.GenericTypeRegistryProvider.RegisterClosedPreProcessorFactories(ClosedPreProcessorFactories);
                    FastEndpoints.GenericTypeRegistryProvider.RegisterClosedPostProcessorFactories(ClosedPostProcessorFactories);
                    // Note: CommandHandlerExecutor<,> is internal, so we use a string-based registration
                    FastEndpoints.GenericTypeRegistryProvider.RegisterCommandHandlerExecutorTypePairs(CommandHandlerExecutorTypePairs);
                    FastEndpoints.GenericTypeRegistryProvider.RegisterCommandHandlerInterfaces(CommandHandlerInterfaces);
                    FastEndpoints.GenericTypeRegistryProvider.RegisterEventBusTypes(EventBusTypes);
                    FastEndpoints.GenericTypeRegistryProvider.RegisterEventHandlers(EventHandlers);
                    // Note: JobQueueTypes and JobQueueFactories not registered - JobQueue<> is internal
                }
            }
            """);

        return sb.ToString();
    }

    /// <summary>
    /// Gets the base type name from an open generic type display string.
    /// E.g., "Namespace.Processor&lt;TReq&gt;" -> "Namespace.Processor"
    /// </summary>
    static string GetOpenGenericBaseTypeName(string openGenericTypeName)
    {
        var angleBracketIndex = openGenericTypeName.IndexOf('<');
        return angleBracketIndex > 0 ? openGenericTypeName.Substring(0, angleBracketIndex) : openGenericTypeName;
    }

    /// <summary>
    /// Converts a generic type display string to unbounded generic syntax for typeof().
    /// E.g., "Namespace.MyType&lt;TReq&gt;" with 1 type param -> "Namespace.MyType&lt;&gt;"
    /// E.g., "Namespace.MyType&lt;TReq, TRes&gt;" with 2 type params -> "Namespace.MyType&lt;,&gt;"
    /// </summary>
    static string ToUnboundedGenericSyntax(string typeName, int typeParamCount)
    {
        var baseName = GetOpenGenericBaseTypeName(typeName);
        // Generate the appropriate number of commas for unbounded generic
        // e.g., 1 type param = "<>", 2 type params = "<,>", 3 type params = "<,,>"
        var commas = typeParamCount > 1 ? new string(',', typeParamCount - 1) : "";
        return baseName + "<" + commas + ">";
    }
}
