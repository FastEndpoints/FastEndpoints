using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints.OpenApi;

sealed class ChildValidatorResolver(IServiceResolver serviceResolver,
                                    ILogger<ValidationSchemaTransformer>? logger,
                                    Func<IServiceScope> createScope) : IDisposable
{
    IServiceScope? _scope;

    IServiceProvider Services => (_scope ??= createScope()).ServiceProvider;

    public void Dispose()
        => _scope?.Dispose();

    public void ApplyChildValidator(IPropertyValidator propertyValidator, HashSet<Type> activeChildValidators, Action<IValidator> applyValidator)
    {
        if (!TryCreateChildValidator(propertyValidator, activeChildValidators, out var validatorType, out var childValidator))
            return;

        try
        {
            applyValidator(childValidator);
        }
        finally
        {
            activeChildValidators.Remove(validatorType);
        }
    }

    [UnconditionalSuppressMessage("aot", "IL2075")]
    bool TryCreateChildValidator(IPropertyValidator propertyValidator,
                                 HashSet<Type> activeChildValidators,
                                 [NotNullWhen(true)] out Type? validatorType,
                                 [NotNullWhen(true)] out IValidator? childValidator)
    {
        validatorType = null;
        childValidator = null;

        var propertyValidatorType = propertyValidator.GetType();

        if (propertyValidatorType.Name.StartsWith("PolymorphicValidator"))
        {
            logger?.SwaggerWithFluentValidationIntegrationForPolymorphicValidatorsIsNotSupported(propertyValidatorType.Name);

            return false;
        }

        var validatorTypeObj = propertyValidatorType.GetProperty("ValidatorType")?.GetValue(propertyValidator);

        if (validatorTypeObj is not Type resolvedValidatorType)
            throw new InvalidOperationException("ChildValidatorAdaptor.ValidatorType is null");

        if (resolvedValidatorType.IsInterface || !activeChildValidators.Add(resolvedValidatorType))
            return false;

        validatorType = resolvedValidatorType;
        childValidator = (IValidator)serviceResolver.CreateInstance(resolvedValidatorType, Services);

        return true;
    }
}