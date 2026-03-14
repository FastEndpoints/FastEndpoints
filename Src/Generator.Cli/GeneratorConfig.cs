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

    public string[] SkipTypes { get; } =
    [
        "EmptyRequest",
        "EmptyResponse"
    ];

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

    private GeneratorConfig() { }
}