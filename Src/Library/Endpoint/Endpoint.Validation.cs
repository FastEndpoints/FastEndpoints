using System.Collections;
using FluentValidation;
using FluentValidation.Results;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;

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
        {
            ValidateObjectRecursively(req, string.Empty, validationFailures, []);
        }

        if (validationFailures.Count > 0 && def.ThrowIfValidationFails)
            throw new ValidationFailureException(validationFailures, "Request validation failed");
    }

    static void ValidateObjectRecursively(object obj, string propertyPath, List<ValidationFailure> validationFailures, HashSet<object> visitedObjects)
    {
        // Prevent infinite recursion for circular references
        if (visitedObjects.Contains(obj))
            return;

        var objType = obj.GetType();

        // Skip primitive types, strings, and value types that don't need validation
        if (objType.IsPrimitive || objType == typeof(string) || objType == typeof(DateTime) ||
            objType == typeof(DateTimeOffset) || objType == typeof(TimeSpan) || objType == typeof(Guid) ||
            objType.IsEnum || (objType.IsValueType && !HasValidationAttributes(objType)))
            return;

        visitedObjects.Add(obj);

        try
        {
            // Validate the current object
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(obj);

            if (!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(obj, validationContext, validationResults, true))
            {
                foreach (var res in validationResults)
                {
                    var memberName = res.MemberNames.FirstOrDefault();
                    var fullPropertyPath = string.IsNullOrEmpty(propertyPath) ? memberName : $"{propertyPath}.{memberName}";
                    validationFailures.AddError(new(fullPropertyPath, res.ErrorMessage), string.Empty);
                }
            }

            // Recursively validate properties
            var properties = objType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && p.GetIndexParameters().Length == 0); // Exclude indexers

            foreach (var property in properties)
            {
                var propertyValue = property.GetValue(obj);
                if (propertyValue is null)
                    continue;

                var currentPropertyPath = string.IsNullOrEmpty(propertyPath) ? property.Name : $"{propertyPath}.{property.Name}";

                // Handle collections (IEnumerable but not string)
                if (propertyValue is IEnumerable enumerable and not string)
                {
                    var index = 0;
                    foreach (var item in enumerable)
                    {
                        if (item is not null)
                        {
                            var itemPath = $"{currentPropertyPath}[{index}]";
                            ValidateObjectRecursively(item, itemPath, validationFailures, visitedObjects);
                        }
                        index++;
                    }
                }
                else
                {
                    // Handle nested objects
                    ValidateObjectRecursively(propertyValue, currentPropertyPath, validationFailures, visitedObjects);
                }
            }
        }
        finally
        {
            visitedObjects.Remove(obj);
        }
    }

    static bool HasValidationAttributes(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                   .Any(p => p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.ValidationAttribute), true).Any()) ||
               type.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.ValidationAttribute), true).Any();
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