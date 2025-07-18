using System.Text.Json;

namespace FastEndpoints.DTOs;

public record StreamItem(string Id, string EventName, object? Data)
{
    public virtual string GetDataString(JsonSerializerOptions options)
    {
        return JsonSerializer.Serialize(Data, options);
    }
}
