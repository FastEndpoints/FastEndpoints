using System.Text.Json;

namespace FastEndpoints;

public class StreamItem
{
    /// <summary>event id</summary>
    public string? Id { get; init; }

    /// <summary>event name</summary>
    public string EventName { get; init; }

    /// <summary>event data</summary>
    public object Data { get; init; }

    /// <summary>reconnection time in milliseconds</summary>
    public int? Retry { get; init; }

    /// <param name="eventName">the name of the event</param>
    /// <param name="data">the event data</param>
    public StreamItem(string eventName, object data)
    {
        EventName = eventName;
        Data = data;
    }

    /// <summary>
    /// </summary>
    /// <param name="id">the id of the event</param>
    /// <param name="eventName">the name of the event</param>
    /// <param name="data">the event data</param>
    /// <param name="retry">reconnection time in milliseconds</param>
    public StreamItem(string? id, string eventName, object data, int? retry = null)
    {
        Id = id;
        EventName = eventName;
        Data = data;
        Retry = retry;
    }

    /// <summary>
    /// override this method in order to take control of the serialization of the event data
    /// </summary>
    /// <param name="options">json serializer options</param>
    public virtual string GetDataString(JsonSerializerOptions options)
        => JsonSerializer.Serialize(Data, options);
}