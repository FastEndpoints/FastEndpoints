namespace FastEndpoints;

/// <summary>
/// Central provider for source-generated type registrations.
/// Used to provide AOT-compatible type lookups.
/// </summary>
public static class GenericTypeRegistryProvider
{
    static readonly Dictionary<Type, Type> _commandHandlers = new();
    static readonly Dictionary<Type, List<Type>> _preProcessors = new();
    static readonly Dictionary<Type, List<Type>> _postProcessors = new();
    static readonly Dictionary<Type, (Type RequestType, Type ResponseType)> _endpointTypes = new();
    static readonly Dictionary<Type, Type> _listTypes = new();
    static readonly Dictionary<(Type OpenGeneric, Type Request), Type> _closedPreProcessors = new();
    static readonly Dictionary<(Type OpenGeneric, Type Request, Type Response), Type> _closedPostProcessors = new();
    static readonly Dictionary<(Type Command, Type Result), Type> _commandHandlerExecutors = new();
    static readonly Dictionary<Type, Type> _commandHandlerInterfaces = new();
    static readonly Dictionary<(Type Command, Type Result, Type StorageRecord, Type StorageProvider), Type> _jobQueueTypes = new();
    static readonly Dictionary<(Type Command, Type Result, Type StorageRecord, Type StorageProvider), Func<IServiceProvider, object>> _jobQueueFactories = new();
    static readonly Dictionary<Type, Type> _eventBusTypes = new();
    static readonly Dictionary<(Type OpenGeneric, Type Request), Type> _closedRequestBinders = new();
    static readonly Dictionary<Type, List<Type>> _eventHandlers = new();
    static readonly Dictionary<(Type OpenGeneric, Type Request), Func<object>> _closedPreProcessorFactories = new();
    static readonly Dictionary<(Type OpenGeneric, Type Request, Type Response), Func<object>> _closedPostProcessorFactories = new();

    /// <summary>
    /// Registers type information from a source-generated assembly.
    /// Called by module initializers in generated code.
    /// </summary>
    public static void RegisterFromAssembly(
        Dictionary<Type, Type> commandHandlers,
        Dictionary<Type, List<Type>> preProcessors,
        Dictionary<Type, List<Type>> postProcessors,
        Dictionary<Type, (Type RequestType, Type ResponseType)> endpointTypes)
    {
        foreach (var kvp in commandHandlers)
        {
            _commandHandlers.TryAdd(kvp.Key, kvp.Value);
        }

        foreach (var kvp in preProcessors)
        {
            if (_preProcessors.TryGetValue(kvp.Key, out var existing))
                existing.AddRange(kvp.Value);
            else
                _preProcessors[kvp.Key] = new List<Type>(kvp.Value);
        }

        foreach (var kvp in postProcessors)
        {
            if (_postProcessors.TryGetValue(kvp.Key, out var existing))
                existing.AddRange(kvp.Value);
            else
                _postProcessors[kvp.Key] = new List<Type>(kvp.Value);
        }

        foreach (var kvp in endpointTypes)
        {
            _endpointTypes.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Registers pre-computed List&lt;T&gt; type mappings.
    /// </summary>
    public static void RegisterListTypes(Dictionary<Type, Type> listTypes)
    {
        foreach (var kvp in listTypes)
        {
            _listTypes.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Registers pre-computed closed pre-processor type mappings.
    /// </summary>
    public static void RegisterClosedPreProcessors(Dictionary<(Type OpenGeneric, Type Request), Type> closedProcessors)
    {
        foreach (var kvp in closedProcessors)
        {
            _closedPreProcessors.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Registers pre-computed closed post-processor type mappings.
    /// </summary>
    public static void RegisterClosedPostProcessors(Dictionary<(Type OpenGeneric, Type Request, Type Response), Type> closedProcessors)
    {
        foreach (var kvp in closedProcessors)
        {
            _closedPostProcessors.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Registers factory functions for creating closed pre-processor instances.
    /// </summary>
    public static void RegisterClosedPreProcessorFactories(Dictionary<(Type OpenGeneric, Type Request), Func<object>> factories)
    {
        foreach (var kvp in factories)
        {
            _closedPreProcessorFactories.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Registers factory functions for creating closed post-processor instances.
    /// </summary>
    public static void RegisterClosedPostProcessorFactories(Dictionary<(Type OpenGeneric, Type Request, Type Response), Func<object>> factories)
    {
        foreach (var kvp in factories)
        {
            _closedPostProcessorFactories.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Tries to get the handler type for a command.
    /// </summary>
    public static bool TryGetCommandHandler(Type commandType, out Type? handlerType)
        => _commandHandlers.TryGetValue(commandType, out handlerType);

    /// <summary>
    /// Tries to get pre-processor types for a request type.
    /// </summary>
    public static bool TryGetPreProcessors(Type requestType, out List<Type>? processorTypes)
        => _preProcessors.TryGetValue(requestType, out processorTypes);

    /// <summary>
    /// Tries to get post-processor types for a request type.
    /// </summary>
    public static bool TryGetPostProcessors(Type requestType, out List<Type>? processorTypes)
        => _postProcessors.TryGetValue(requestType, out processorTypes);

    /// <summary>
    /// Tries to get the request/response types for an endpoint type.
    /// </summary>
    public static bool TryGetEndpointTypes(Type endpointType, out (Type RequestType, Type ResponseType) types)
        => _endpointTypes.TryGetValue(endpointType, out types);

    /// <summary>
    /// Tries to get a pre-computed List&lt;T&gt; type for an element type.
    /// </summary>
    public static bool TryGetListType(Type elementType, out Type? listType)
        => _listTypes.TryGetValue(elementType, out listType);

    /// <summary>
    /// Tries to get a pre-computed closed pre-processor type.
    /// </summary>
    public static bool TryGetClosedPreProcessor(Type openGenericProcessor, Type requestType, out Type? closedType)
        => _closedPreProcessors.TryGetValue((openGenericProcessor, requestType), out closedType);

    /// <summary>
    /// Tries to get a pre-computed closed post-processor type.
    /// </summary>
    public static bool TryGetClosedPostProcessor(Type openGenericProcessor, Type requestType, Type responseType, out Type? closedType)
        => _closedPostProcessors.TryGetValue((openGenericProcessor, requestType, responseType), out closedType);

    /// <summary>
    /// Tries to create a closed pre-processor instance using an AOT-safe factory.
    /// </summary>
    public static bool TryCreateClosedPreProcessor(Type openGenericProcessor, Type requestType, out object? instance)
    {
        if (_closedPreProcessorFactories.TryGetValue((openGenericProcessor, requestType), out var factory))
        {
            instance = factory();
            return true;
        }
        instance = null;
        return false;
    }

    /// <summary>
    /// Tries to create a closed post-processor instance using an AOT-safe factory.
    /// </summary>
    public static bool TryCreateClosedPostProcessor(Type openGenericProcessor, Type requestType, Type responseType, out object? instance)
    {
        if (_closedPostProcessorFactories.TryGetValue((openGenericProcessor, requestType, responseType), out var factory))
        {
            instance = factory();
            return true;
        }
        instance = null;
        return false;
    }

    /// <summary>
    /// Tries to create a pre-processor instance from its closed generic type using an AOT-safe factory.
    /// </summary>
    /// <param name="closedProcessorType">The closed generic processor type (e.g., SecurityProcessor&lt;Request&gt;).</param>
    /// <param name="instance">The created processor instance if found.</param>
    /// <returns>True if the factory was found and instance created; otherwise false.</returns>
    public static bool TryCreatePreProcessorFromClosedType(Type closedProcessorType, out object? instance)
    {
        if (closedProcessorType.IsGenericType && !closedProcessorType.IsGenericTypeDefinition)
        {
            var openGeneric = closedProcessorType.GetGenericTypeDefinition();
            var genericArgs = closedProcessorType.GetGenericArguments();
            if (genericArgs.Length >= 1)
            {
                var requestType = genericArgs[0];
                if (_closedPreProcessorFactories.TryGetValue((openGeneric, requestType), out var factory))
                {
                    instance = factory();
                    return true;
                }
            }
        }
        instance = null;
        return false;
    }

    /// <summary>
    /// Tries to create a post-processor instance from its closed generic type using an AOT-safe factory.
    /// </summary>
    /// <param name="closedProcessorType">The closed generic processor type (e.g., MyPostProcessor&lt;Request, Response&gt;).</param>
    /// <param name="instance">The created processor instance if found.</param>
    /// <returns>True if the factory was found and instance created; otherwise false.</returns>
    public static bool TryCreatePostProcessorFromClosedType(Type closedProcessorType, out object? instance)
    {
        if (closedProcessorType.IsGenericType && !closedProcessorType.IsGenericTypeDefinition)
        {
            var openGeneric = closedProcessorType.GetGenericTypeDefinition();
            var genericArgs = closedProcessorType.GetGenericArguments();
            if (genericArgs.Length >= 2)
            {
                var requestType = genericArgs[0];
                var responseType = genericArgs[1];
                if (_closedPostProcessorFactories.TryGetValue((openGeneric, requestType, responseType), out var factory))
                {
                    instance = factory();
                    return true;
                }
            }
        }
        instance = null;
        return false;
    }

    /// <summary>
    /// Registers pre-computed command handler executor type pairs.
    /// The actual CommandHandlerExecutor types are constructed using the provided open generic type.
    /// </summary>
    /// <param name="openGenericExecutorType">The open generic CommandHandlerExecutor&lt;,&gt; type.</param>
    /// <param name="typePairs">The command and result type pairs.</param>
    public static void RegisterCommandHandlerExecutorTypePairs(Type openGenericExecutorType, List<(Type Command, Type Result)> typePairs)
    {
        foreach (var (command, result) in typePairs)
        {
            // This MakeGenericType is safe as it's called at startup with source-generated type information
            var executorType = openGenericExecutorType.MakeGenericType(command, result);
            _commandHandlerExecutors.TryAdd((command, result), executorType);
        }
    }

    /// <summary>
    /// Registers pre-computed command handler executor type pairs using the internal CommandHandlerExecutor type.
    /// This overload is used when the generator cannot reference the internal type directly.
    /// </summary>
    /// <param name="typePairs">The command and result type pairs.</param>
    public static void RegisterCommandHandlerExecutorTypePairs(List<(Type Command, Type Result)> typePairs)
    {
        // The CommandHandlerExecutor<,> type is resolved at runtime in the Library assembly
        // where it's accessible. This method just stores the type pairs for later resolution.
        foreach (var (command, result) in typePairs)
        {
            _commandHandlerExecutors.TryAdd((command, result), null!); // Placeholder - actual type resolved later
        }
    }

    /// <summary>
    /// Registers pre-computed command handler interface type mappings.
    /// </summary>
    public static void RegisterCommandHandlerInterfaces(Dictionary<Type, Type> interfaces)
    {
        foreach (var kvp in interfaces)
        {
            _commandHandlerInterfaces.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Registers pre-computed job queue type mappings.
    /// </summary>
    public static void RegisterJobQueueTypes(Dictionary<(Type Command, Type Result, Type StorageRecord, Type StorageProvider), Type> jobQueues)
    {
        foreach (var kvp in jobQueues)
        {
            _jobQueueTypes.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Registers factory functions for creating job queue instances.
    /// These direct new() calls ensure the AOT compiler preserves the constructors.
    /// </summary>
    public static void RegisterJobQueueFactories(Dictionary<(Type Command, Type Result, Type StorageRecord, Type StorageProvider), Func<IServiceProvider, object>> factories)
    {
        foreach (var kvp in factories)
        {
            _jobQueueFactories.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Registers pre-computed EventBus&lt;T&gt; type mappings.
    /// </summary>
    public static void RegisterEventBusTypes(Dictionary<Type, Type> eventBuses)
    {
        foreach (var kvp in eventBuses)
        {
            _eventBusTypes.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Registers pre-computed closed request binder type mappings.
    /// </summary>
    public static void RegisterClosedRequestBinders(Dictionary<(Type OpenGeneric, Type Request), Type> binders)
    {
        foreach (var kvp in binders)
        {
            _closedRequestBinders.TryAdd(kvp.Key, kvp.Value);
        }
    }

    /// <summary>
    /// Tries to get a pre-computed CommandHandlerExecutor&lt;TCommand, TResult&gt; type.
    /// </summary>
    public static bool TryGetCommandHandlerExecutor(Type commandType, Type resultType, out Type? executorType)
        => _commandHandlerExecutors.TryGetValue((commandType, resultType), out executorType);

    /// <summary>
    /// Tries to get a pre-computed ICommandHandler&lt;TCommand&gt; or ICommandHandler&lt;TCommand, TResult&gt; interface type.
    /// </summary>
    public static bool TryGetCommandHandlerInterface(Type commandType, out Type? interfaceType)
        => _commandHandlerInterfaces.TryGetValue(commandType, out interfaceType);

    /// <summary>
    /// Tries to get a pre-computed JobQueue&lt;TCommand, TResult, TStorageRecord, TStorageProvider&gt; type.
    /// </summary>
    public static bool TryGetJobQueueType(Type commandType, Type resultType, Type storageRecordType, Type storageProviderType, out Type? jobQueueType)
        => _jobQueueTypes.TryGetValue((commandType, resultType, storageRecordType, storageProviderType), out jobQueueType);

    /// <summary>
    /// Tries to create a JobQueue instance using a pre-registered factory.
    /// </summary>
    public static bool TryCreateJobQueue(Type commandType, Type resultType, Type storageRecordType, Type storageProviderType, IServiceProvider serviceProvider, out object? jobQueue)
    {
        if (_jobQueueFactories.TryGetValue((commandType, resultType, storageRecordType, storageProviderType), out var factory))
        {
            jobQueue = factory(serviceProvider);
            return true;
        }
        jobQueue = null;
        return false;
    }

    /// <summary>
    /// Tries to get a pre-computed EventBus&lt;TEvent&gt; type.
    /// </summary>
    public static bool TryGetEventBusType(Type eventType, out Type? eventBusType)
        => _eventBusTypes.TryGetValue(eventType, out eventBusType);

    /// <summary>
    /// Tries to get a pre-computed closed request binder type.
    /// </summary>
    public static bool TryGetClosedRequestBinder(Type openGenericBinder, Type requestType, out Type? closedType)
        => _closedRequestBinders.TryGetValue((openGenericBinder, requestType), out closedType);

    /// <summary>
    /// Registers pre-computed event handler type mappings.
    /// </summary>
    public static void RegisterEventHandlers(Dictionary<Type, List<Type>> eventHandlers)
    {
        foreach (var kvp in eventHandlers)
        {
            if (_eventHandlers.TryGetValue(kvp.Key, out var existing))
                existing.AddRange(kvp.Value);
            else
                _eventHandlers[kvp.Key] = new List<Type>(kvp.Value);
        }
    }

    /// <summary>
    /// Tries to get event handler types for an event type.
    /// </summary>
    public static bool TryGetEventHandlers(Type eventType, out List<Type>? handlerTypes)
        => _eventHandlers.TryGetValue(eventType, out handlerTypes);

    /// <summary>
    /// Gets all pre-registered job queue types for a specific storage record and provider.
    /// </summary>
    public static IEnumerable<Type> GetJobQueueTypesForStorage(Type storageRecordType, Type storageProviderType)
    {
        foreach (var kvp in _jobQueueTypes)
        {
            if (kvp.Key.StorageRecord == storageRecordType && kvp.Key.StorageProvider == storageProviderType)
                yield return kvp.Value;
        }
    }
}
