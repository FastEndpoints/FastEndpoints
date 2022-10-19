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
