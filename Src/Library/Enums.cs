namespace FastEndpoints;

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

/// <summary>
/// enum used to specify whether to execute global pre/post processors before endpoint level processors
/// </summary>
public enum Order
{
    /// <summary>
    /// execute global processors before the endpoint level processors
    /// </summary>
    Before = 0,

    /// <summary>
    /// execute global processors after the endpoint level processors
    /// </summary>
    After = 1
}