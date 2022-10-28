namespace FastEndpoints;

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