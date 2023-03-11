namespace FastEndpoints;

/// <summary>
/// the dto used to send an error response to the client when an unhandled exception occurs on the server
/// </summary>
public sealed class InternalErrorResponse
{
    /// <summary>
    /// error status
    /// </summary>
    public string Status { get; set; } = "Internal Server Error!";

    /// <summary>
    /// http status code of the error response
    /// </summary>
    public int Code { get; set; }

    /// <summary>
    /// the reason for the error
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// additional information or instructions
    /// </summary>
    public string Note { get; set; }
}