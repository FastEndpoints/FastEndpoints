namespace FastEndpoints;

/// <summary>
/// enum for specifying a http verb
/// </summary>
public enum Http
{
    /// <summary>
    /// retrieve a record
    /// </summary>
    GET = 1,

    /// <summary>
    /// create a record
    /// </summary>
    POST = 2,

    /// <summary>
    /// replace a record
    /// </summary>
    PUT = 3,

    /// <summary>
    /// partially update a record
    /// </summary>
    PATCH = 4,

    /// <summary>
    /// remove a record
    /// </summary>
    DELETE = 5,

    /// <summary>
    /// retrieve only headers
    /// </summary>
    HEAD = 6
}
