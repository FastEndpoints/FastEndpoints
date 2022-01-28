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
    DELETE = 5
}

/// <summary>
/// enum for specifying the waiting mode for event notifications
/// </summary>
public enum Mode
{
    /// <summary>
    /// returns an already completed Task (fire and forget)
    /// </summary>
    WaitForNone = 0,

    /// <summary>
    /// returns a Task that will complete when any of the subscribers complete their work
    /// </summary>
    WaitForAny = 1,

    /// <summary>
    /// return a Task that will complete only when all of the subscribers complete their work
    /// </summary>
    WaitForAll = 2
}
