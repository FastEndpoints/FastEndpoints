namespace FastEndpoints.Validation;

/// <summary>
/// the exception thrown when a validation error has occured. this class is only useful in unit tests.
/// </summary>
public class ValidationFailureException : Exception
{
    public ValidationFailureException() { }

    public ValidationFailureException(string? message) : base(message) { }

    public ValidationFailureException(string? message, Exception? innerException) : base(message, innerException) { }
}