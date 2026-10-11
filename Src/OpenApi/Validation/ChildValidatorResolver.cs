using System.Diagnostics.CodeAnalysis;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using static FastEndpoints.OpenApi.ValidationSchemaMutation;

namespace FastEndpoints.OpenApi;

sealed class ChildValidatorResolver(IServiceResolver serviceResolver,
                                    OperationSchemaMutationContext mutationCtx,
                                    ILogger<ValidationSchemaTransformer>? logger,
                                    Func<IServiceScope> createScope,
                                    Action<OpenApiSchema, IValidator, string, HashSet<Type>> applyValidator,
                                    string schemaKey,
                                    bool localizeReferencedSchemas) : IDisposable
{
    IServiceScope? _scope;

    IServiceProvider Services => (_scope ??= createScope()).ServiceProvider;

    public void Dispose()
        => _scope?.Dispose();

    public void ApplyChildValidator(OpenApiSchema schema, string propertyName, IPropertyValidator propertyValidator, HashSet<Type> activeChildValidators)
    {
        if (!TryCreateChildValidator(propertyValidator, activeChildValidators, out var validatorType, out var childValidator))
            return;

        try
        {
            if (schema.Properties?.TryGetValue(propertyName, out var childPropSchema) == true)
            {
                var childSchema = ResolveForMutation(
                    childPropSchema,
                    localizeReferencedSchemas,
                    mutationCtx,
                    $"{schemaKey}.{propertyName}",
                    localized => schema.Properties![propertyName] = localized);

                if (childSchema is not null)
                {
                    if (ResolveArrayItemsForMutation(
                            childSchema,
                            localizeReferencedSchemas,
                            mutationCtx,
                            schemaKey,
                            propertyName) is { } itemsSchema)
                        applyValidator(itemsSchema, childValidator, string.Empty, activeChildValidators);
                    else
                        applyValidator(childSchema, childValidator, string.Empty, activeChildValidators);
                }
            }
        }
        finally
        {
            activeChildValidators.Remove(validatorType);
        }
    }

    public void ApplyChildValidator(OpenApiSchema schema, IPropertyValidator propertyValidator, HashSet<Type> activeChildValidators)
    {
        if (TryCreateChildValidator(propertyValidator, activeChildValidators, out var validatorType, out var childValidator))
        {
            try
            {
                applyValidator(schema, childValidator, string.Empty, activeChildValidators);
            }
            finally
            {
                activeChildValidators.Remove(validatorType);
            }
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