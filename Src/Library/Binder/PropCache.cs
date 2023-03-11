namespace FastEndpoints;

internal class PropCache
{
    public Type PropType { get; init; }
    public Action<object, object?> PropSetter { get; init; }
}

internal class PrimaryPropCacheEntry : PropCache
{
    public Func<object?, ParseResult> ValueParser { get; init; }
}

internal sealed class SecondaryPropCacheEntry : PrimaryPropCacheEntry
{
    public string Identifier { get; init; }
    public bool ForbidIfMissing { get; init; }
    public string? PropName { get; set; }
    public bool IsCollection { get; set; }
}