namespace FastEndpoints;

/// <summary>
/// enum for specifying the waiting mode for event notifications
/// </summary>
public enum Mode
{
    /// <summary>
    /// returns an already completed Task (fire and forget)
    /// <para>WARNING: exceptions cannot be captured by caller</para>
    /// </summary>
    WaitForNone = 0,

    /// <summary>
    /// returns a Task that will complete when any of the subscribers complete their work
    /// <para>WARNING: exceptions cannot be captured by caller</para>
    /// </summary>
    WaitForAny = 1,

    /// <summary>
    /// return a Task that will complete only when all of the subscribers complete their work.
    /// <para>HINT: exceptions can be captured by caller</para>
    /// </summary>
    WaitForAll = 2
}
