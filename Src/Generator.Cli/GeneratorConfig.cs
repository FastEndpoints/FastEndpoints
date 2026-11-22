namespace FastEndpoints.Generator.Cli;

sealed class GeneratorConfig
{
    public static readonly GeneratorConfig Instance = new();

    public HashSet<string> RootTypeSkipNames { get; } = new(StringComparer.Ordinal)
    {
        "PlainTextRequest"
    };

    public string[] EndpointBaseTypePatterns { get; } =
    [
        "Endpoint",
        "EndpointWithoutRequest",
        "EndpointWithMapper",
        "EndpointWithMapping",
        "Ep"
    ];

    public string[] ExcludedBaseTypes { get; } =
    [
        "Mapper",
        "Validator",
        "AbstractValidator",
        "Summary",
        "EndpointSummary",
        "ICommand",
        "ICommandHandler",
        "IEvent",
        "IEventHandler"
    ];

    public string[] SkipNamespaces { get; } =
    [
        "Accessibility",
        "FastEndpoints",
        "FluentValidation",
        "Grpc",
        "JetBrains",
        "Microsoft",
        "mscorlib",
        "Namotion",
        "netstandard",
        "Newtonsoft",
        "NJsonSchema",
        "NSwag",
        "NuGet",
        "PresentationCore",
        "PresentationFramework",
        "StackExchange",
        "System",
        "testhost",
        "WindowsBase",
        "YamlDotNet"
    ];

    public HashSet<string> SkipTypes { get; } = new(StringComparer.Ordinal)
    {
        "EmptyRequest",
        "EmptyResponse"
    };

    public HashSet<string> BuiltInCollectionTypes { get; } = new(StringComparer.Ordinal)
    {
        "Dictionary",
        "IEnumerable",
        "List",
        "ICollection",
        "IDictionary",
        "IList",
        "IReadOnlyDictionary",
        "IReadOnlyList",
        "IReadOnlyCollection",
        "HashSet",
        "SortedSet",
        "Stack",
        "Queue"
    };

    //bcl/framework types that never resolve to anything the generator emits. skipping them avoids loading referenced projects and nuget metadata.
    public HashSet<string> WellKnownExternalTypes { get; } = new(StringComparer.Ordinal)
    {
        "DateTime",
        "DateTimeOffset",
        "DateOnly",
        "TimeOnly",
        "TimeSpan",
        "Guid",
        "Half",
        "Int128",
        "UInt128",
        "IFormFile",
        "IFormFileCollection",
        "IResult",
        "Results",
        "JsonElement",
        "JsonNode",
        "JsonDocument",
        "Stream",
        "CancellationToken"
    };

    //single owner of the name-based skip policy. it decides both what gets emitted and what may trigger external type loading.
    public bool IsSkippedNamespace(string typeName)
    {
        typeName = TypeNames.StripGlobal(typeName);

        foreach (var skipNs in SkipNamespaces)
        {
            if (typeName.StartsWith(skipNs + ".", StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    public bool IsSkippedTypeName(string simpleNameWithoutArity)
        => SkipTypes.Contains(simpleNameWithoutArity);

    private GeneratorConfig() { }
}