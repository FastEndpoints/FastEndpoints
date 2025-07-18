using System.Text.Json;

namespace FastEndpoints;

/// <summary>
/// </summary>
/// <param name="id">the id of the event</param>
/// <param name="eventName">the name of the event</param>
/// <param name="data">the event data</param>
public class StreamItem(string id, string eventName, object? data)
{
    /// <summary>event id</summary>
    public string Id { get; init; } = id;

    /// <summary>event name</summary>
    public string EventName { get; init; } = eventName;

    /// <summary>event data</summary>
    public object? Data { get; init; } = data;

    /// <summary>
    /// override this method in order to take control of the serialization of the event data
    /// </summary>
    /// <param name="options">json serializer options</param>
    public virtual string GetDataString(JsonSerializerOptions options)
        => JsonSerializer.Serialize(Data, options);
}