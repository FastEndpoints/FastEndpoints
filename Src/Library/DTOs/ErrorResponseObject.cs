using FluentValidation;

namespace FastEndpoints;

/// <summary>
/// the dto used to send an error response to the client
/// </summary>
public sealed class ErrorResponseObject
{
    public string ErrorCode { get; set; }
    
    public string Message { get; set; }
    
    public Severity Severity { get; set; }
}