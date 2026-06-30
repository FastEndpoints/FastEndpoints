using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;

namespace FastEndpoints;

static class ValidationExtensions
{
    extension(List<ValidationFailure> failures)
    {
        internal bool ValidationFailed()
            => failures.Count > 0;

        internal void AddError(ValidationFailure failure, string? reqDtoFromBodyPropName = null)
        {
            if (reqDtoFromBodyPropName?.Length > 1 && failure.PropertyName.StartsWith(reqDtoFromBodyPropName))
            {
                failure.PropertyName = failure.PropertyName.Substring(
                    reqDtoFromBodyPropName.Length,
                    failure.PropertyName.Length - reqDtoFromBodyPropName.Length);
            }

            failures.Add(failure);
        }

        internal void AddError(string message, string? errorCode = null, Severity severity = Severity.Error)
        {
            failures.AddError(
                new(Cfg.ErrOpts.GeneralErrorsField, message)
                {
                    ErrorCode = errorCode,
                    Severity = severity
                },
                null);
        }

        internal void AddError<T>(Expression<Func<T, object?>> property,
                                  string errorMessage,
                                  string? errorCode = null,
                                  Severity severity = Severity.Error,
                                  string? reqDtoFromBodyPropName = null)
        {
            failures.AddError(
                new(property.Body.GetPropertyChain(), errorMessage)
                {
                    ErrorCode = errorCode,
                    Severity = severity
                },
                reqDtoFromBodyPropName);
        }

        internal void ThrowIfAnyErrors(int? statusCode)
        {
            if (failures.Count > 0)
                throw new ValidationFailureException(failures, $"{nameof(ThrowIfAnyErrors)}() called") { StatusCode = statusCode };
        }

        [DoesNotReturn]
        internal void ThrowError(ValidationFailure failure, int? statusCode, string? reqDtoFromBodyPropName = null)
        {
            failure.PropertyName ??= Cfg.ErrOpts.GeneralErrorsField;

            failures.AddError(failure, reqDtoFromBodyPropName);

            throw new ValidationFailureException(failures, $"{nameof(ThrowError)}() called!") { StatusCode = statusCode };
        }

        [DoesNotReturn]
        internal void ThrowError(int? statusCode,
                                 string errorMessage,
                                 string? errorCode = null,
                                 Severity severity = Severity.Error)
        {
            failures.AddError(errorMessage, errorCode, severity);

            throw new ValidationFailureException(failures, $"{nameof(ThrowError)}() called!") { StatusCode = statusCode };
        }

        [DoesNotReturn]
        internal void ThrowError<T>(Expression<Func<T, object?>> property,
                                    int? statusCode,
                                    string errorMessage,
                                    string? errorCode = null,
                                    Severity severity = Severity.Error,
                                    string? reqDtoFromBodyPropName = null)
        {
            failures.AddError(property, errorMessage, errorCode, severity, reqDtoFromBodyPropName);

            throw new ValidationFailureException(failures, $"{nameof(ThrowError)}() called") { StatusCode = statusCode };
        }
    }
}