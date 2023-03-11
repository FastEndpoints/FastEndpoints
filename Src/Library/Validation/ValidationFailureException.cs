using FluentValidation.Results;

namespace FastEndpoints;

/// <summary>
/// the exception thrown when validation failure occurs.
/// inspect the `Failures` property for details.
/// </summary>
public sealed class ValidationFailureException : Exception
{
    /// <summary>
    /// the collection of failures that have occured.
    /// </summary>
    public IEnumerable<ValidationFailure>? Failures { get; init; }

    public ValidationFailureException() { }

    public ValidationFailureException(string? message) : base(message) { }

    public ValidationFailureException(string? message, Exception? innerException) : base(message, innerException) { }

    public ValidationFailureException(IEnumerable<ValidationFailure> failures, string message)
        : base($"{message} - {failures.FirstOrDefault()?.ErrorMessage}")
    {
        Failures = failures;
    }
}