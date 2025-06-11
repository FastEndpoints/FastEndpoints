using System.Text.Json.Serialization;

namespace FastEndpoints.OData;

public sealed class ODataResult<TEntity>
{
    [JsonPropertyName("@odata.context")]
    public string Context { get; set; }

    [JsonPropertyName("value")]
    public TEntity[] Value { get; set; }
}