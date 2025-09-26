using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using FluentValidation;
using FluentValidation.Results;
using DAValidationResult = System.ComponentModel.DataAnnotations.ValidationResult;
using DAValidator = System.ComponentModel.DataAnnotations.Validator;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : IValidationErrors<TRequest> where TRequest : notnull
{
    static async Task ValidateRequest(TRequest req, EndpointDefinition def, List<ValidationFailure> validationFailures, CancellationToken cancellation)
    {
        if (def.ValidatorType is null && Cfg.ValOpts.EnableDataAnnotationsSupport is false)
            return;

        if (def.ValidatorType is not null) //use fluent validations
        {
            var valResult = await ((IValidator<TRequest>)def.GetValidator()!).ValidateAsync(req, cancellation);

            if (!valResult.IsValid)
            {
                for (var i = 0; i < valResult.Errors.Count; i++)
                    validationFailures.AddError(valResult.Errors[i], def.ReqDtoFromBodyPropName);
            }
        }
        else if (_tRequest != Types.EmptyRequest) //try data annotations because DA support is enabled by user
            ValidateRecursively(req, string.Empty, validationFailures, []);

        if (validationFailures.Count > 0 && def.ThrowIfValidationFails)
            throw new ValidationFailureException(validationFailures, "Request validation failed");

        return;

        static void ValidateRecursively(object obj, string propertyPath, List<ValidationFailure> validationFailures, HashSet<object> visitedObjects)
        {
            var tObject = obj.GetType();

            if (!tObject.IsValidatable() || !visitedObjects.Add(obj))
                return;

            try
            {
                var validationResults = new List<DAValidationResult>();

                if (!DAValidator.TryValidateObject(obj, new(obj), validationResults, true))
                {
                    foreach (var res in validationResults)
                    {
                        var memberName = res.MemberNames.FirstOrDefault() ?? string.Empty;
                        var fullPropertyPath = AppendToPropertyPath(propertyPath, memberName);
                        validationFailures.AddError(new(fullPropertyPath, res.ErrorMessage), null);
                    }
                }

                foreach (var property in tObject.BindableProps())
                {
                    var propertyValue = property.GetValue(obj); // todo: use a cached compiled expression

                    if (propertyValue is null)
                        continue;

                    var currentPropertyPath = AppendToPropertyPath(propertyPath, property.Name);

                    if (property.PropertyType.IsCollection()) // handle collection property
                    {
                        var index = 0;

                        foreach (var item in (IEnumerable)propertyValue)
                        {
                            if (item is not null)
                            {
                                var itemPath = $"{currentPropertyPath}[{index}]";
                                ValidateRecursively(item, itemPath, validationFailures, visitedObjects);
                            }
                            index++;
                        }
                    }
                    else // handle nested object
                        ValidateRecursively(propertyValue, currentPropertyPath, validationFailures, visitedObjects);
                }
            }
            finally
            {
                visitedObjects.Remove(obj); // if we didn't remove, only the first referenced instance will get an error generated.
            }

            return;

            static string AppendToPropertyPath(string propPath, string propName)
                => string.IsNullOrEmpty(propPath) ? propName : $"{propPath}.{propName}";
        }
    }

    public bool ValidationFailed => ValidationFailures.ValidationFailed();

    public void AddError(ValidationFailure failure)
        => ValidationFailures.AddError(failure, Definition.ReqDtoFromBodyPropName);

    public void AddError(string message, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(message, errorCode, severity);

    public void AddError(Expression<Func<TRequest, object?>> property, string errorMessage, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(property, errorMessage, errorCode, severity, Definition.ReqDtoFromBodyPropName);

    [DoesNotReturn]
    public void ThrowError(ValidationFailure failure, int? statusCode = null)
        => ValidationFailures.ThrowError(failure, statusCode, Definition.ReqDtoFromBodyPropName);

    [DoesNotReturn]
    public void ThrowError(string message, int? statusCode = null)
        => ValidationFailures.ThrowError(statusCode, message);

    [DoesNotReturn]
    public void ThrowError(string message, string errorCode, Severity severity = Severity.Error, int? statusCode = null)
        => ValidationFailures.ThrowError(statusCode, message, errorCode, severity);

    [DoesNotReturn]
    public void ThrowError(Expression<Func<TRequest, object?>> property, string errorMessage, int? statusCode = null)
        => ValidationFailures.ThrowError(property, statusCode, errorMessage, null, default, Definition.ReqDtoFromBodyPropName);

    [DoesNotReturn]
    public void ThrowError(Expression<Func<TRequest, object?>> property,
                           string errorMessage,
                           string errorCode,
                           Severity severity = Severity.Error,
                           int? statusCode = null)
        => ValidationFailures.ThrowError(property, statusCode, errorMessage, errorCode, severity, Definition.ReqDtoFromBodyPropName);

    public void ThrowIfAnyErrors(int? statusCode = null)
        => ValidationFailures.ThrowIfAnyErrors(statusCode);
}