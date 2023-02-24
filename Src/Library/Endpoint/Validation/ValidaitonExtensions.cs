using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FastEndpoints;

internal static class ValidaitonExtensions
{
    internal static bool ValidationFailed(this List<ValidationFailure> failures)
        => failures.Count > 0;

    internal static void AddError(this List<ValidationFailure> failures, string message, string? errorCode = null, Severity severity = Severity.Error)
    {
        var validationFailure = new ValidationFailure(Config.ErrOpts.GeneralErrorsField, message)
        {
            ErrorCode = errorCode,
            Severity = severity
        };

        failures.Add(validationFailure);
    }

    internal static void AddError<T>(this List<ValidationFailure> failures, Expression<Func<T, object>> property, string errorMessage, string? errorCode = null, Severity severity = Severity.Error)
    {
        var validationFailure = new ValidationFailure(property.PropertyName(), errorMessage)
        {
            ErrorCode = errorCode,
            Severity = severity
        };

        failures.Add(validationFailure);
    }

    internal static void ThrowIfAnyErrors(this List<ValidationFailure> failures)
    {
        if (failures.Count > 0)
            throw new ValidationFailureException(failures, $"{nameof(ThrowIfAnyErrors)}() called");
    }

    [DoesNotReturn]
    internal static void ThrowError(this List<ValidationFailure> failures, string message)
    {
        failures.AddError(message);
        throw new ValidationFailureException(failures, $"{nameof(ThrowError)}() called!");
    }

    [DoesNotReturn]
    internal static void ThrowError<T>(this List<ValidationFailure> failures, Expression<Func<T, object>> property, string errorMessage)
    {
        failures.AddError(property, errorMessage);
        throw new ValidationFailureException(failures, $"{nameof(ThrowError)}() called");
    }
}