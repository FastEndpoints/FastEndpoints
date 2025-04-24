﻿using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;

namespace FastEndpoints;

static class ValidationExtensions
{
    internal static bool ValidationFailed(this List<ValidationFailure> failures)
        => failures.Count > 0;

    internal static void AddError(this List<ValidationFailure> failures, ValidationFailure failure, string? reqDtoFromBodyPropName = null)
    {
        if (reqDtoFromBodyPropName?.Length > 1 && failure.PropertyName.StartsWith(reqDtoFromBodyPropName))
        {
            failure.PropertyName = failure.PropertyName.Substring(
                reqDtoFromBodyPropName.Length,
                failure.PropertyName.Length - reqDtoFromBodyPropName.Length);
        }

        failures.Add(failure);
    }

    internal static void AddError(this List<ValidationFailure> failures, string message, string? errorCode = null, Severity severity = Severity.Error)
    {
        failures.AddError(
            new(Cfg.ErrOpts.GeneralErrorsField, message)
            {
                ErrorCode = errorCode,
                Severity = severity
            },
            null);
    }

    internal static void AddError<T>(this List<ValidationFailure> failures,
                                     Expression<Func<T, object?>> property,
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

    internal static void ThrowIfAnyErrors(this List<ValidationFailure> failures, int? statusCode)
    {
        if (failures.Count > 0)
            throw new ValidationFailureException(failures, $"{nameof(ThrowIfAnyErrors)}() called") { StatusCode = statusCode };
    }

    [DoesNotReturn]
    internal static void ThrowError(this List<ValidationFailure> failures, ValidationFailure failure, int? statusCode, string? reqDtoFromBodyPropName = null)
    {
        failure.PropertyName ??= Cfg.ErrOpts.GeneralErrorsField;

        failures.AddError(failure, reqDtoFromBodyPropName);

        throw new ValidationFailureException(failures, $"{nameof(ThrowError)}() called!") { StatusCode = statusCode };
    }

    [DoesNotReturn]
    internal static void ThrowError(this List<ValidationFailure> failures,
                                    int? statusCode,
                                    string errorMessage,
                                    string? errorCode = null,
                                    Severity severity = Severity.Error)
    {
        failures.AddError(errorMessage, errorCode, severity);

        throw new ValidationFailureException(failures, $"{nameof(ThrowError)}() called!") { StatusCode = statusCode };
    }

    [DoesNotReturn]
    internal static void ThrowError<T>(this List<ValidationFailure> failures,
                                       Expression<Func<T, object?>> property,
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