using Microsoft.Extensions.Primitives;

namespace FastEndpoints;

#pragma warning disable CS8618

class PropCache
{
    public Type PropType { get; init; }
    public Action<object, object?> PropSetter { get; init; }
}

class PrimaryPropCacheEntry : PropCache
{
    public Func<StringValues, ParseResult> ValueParser { get; init; }
    public Source? DisabledSources { get; init; }
}

sealed class SecondaryPropCacheEntry : PrimaryPropCacheEntry
{
    public string Identifier { get; init; }
    public bool ForbidIfMissing { get; init; }
    public string? PropName { get; init; }
    public bool IsCollection { get; init; }
}