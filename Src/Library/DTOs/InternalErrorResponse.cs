using System.ComponentModel;

namespace FastEndpoints;

/// <summary>
/// the dto used to send an error response to the client when an unhandled exception occurs on the server
/// </summary>
public sealed class InternalErrorResponse
{
    /// <summary>
    /// error status
    /// </summary>
    [DefaultValue("Internal Server Error!")]
    public string Status { get; set; } = "Internal Server Error!";

    /// <summary>
    /// http status code of the error response
    /// </summary>
    [DefaultValue(500)]
    public int Code { get; set; }

    /// <summary>
    /// the reason for the error
    /// </summary>
    [DefaultValue("Something unexpected has happened")]
    public string Reason { get; set; }

    /// <summary>
    /// additional information or instructions
    /// </summary>
    [DefaultValue("See application log for stack trace.")]
    public string Note { get; set; }
}