namespace FastEndpoints;

internal class PropCache
{
    public Type PropType { get; init; }
    public Action<object, object> PropSetter { get; init; }
}

//NOTE: no point in caching because all reflection happens on each request everytime with this implementation

//internal class QueryPropCacheEntry : PropCache
//{
//    public Action<IReadOnlyDictionary<string, StringValues>, JsonObject> JsonSetter { get; init; }
//}

internal class PrimaryPropCacheEntry : PropCache
{
    public Func<object?, (bool isSuccess, object value)>? ValueParser { get; init; }
}

internal class SecondaryPropCacheEntry : PrimaryPropCacheEntry
{
    public string Identifier { get; init; }
    public bool ForbidIfMissing { get; init; }
    public string? PropName { get; set; }
    public bool IsCollection { get; set; }
}