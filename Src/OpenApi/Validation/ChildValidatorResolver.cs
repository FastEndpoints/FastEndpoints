using System.Diagnostics.CodeAnalysis;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using static FastEndpoints.OpenApi.ValidationSchemaTransformer;

namespace FastEndpoints.OpenApi;

sealed class ChildValidatorResolver(IServiceResolver serviceResolver,
                                    SharedContext sharedCtx,
                                    ILogger<ValidationSchemaTransformer>? logger,
                                    Func<IServiceScope> createScope,
                                    Action<OpenApiSchema, IValidator, string, HashSet<Type>> applyValidator,
                                    Action<OpenApiSchema?, ValidationSchemaApplier.ValidationRuleLookup, string, HashSet<Type>> applyRulesToSchema,
                                    bool usePropertyNamingPolicy,
                                    string operationKey,
                                    string schemaKey,
                                    bool localizeReferencedSchemas) : IDisposable
{
    IServiceScope? _scope;

    IServiceProvider Services => (_scope ??= createScope()).ServiceProvider;

    public void Dispose()
        => _scope?.Dispose();

    public static IEnumerable<IValidator> GetIncludedValidators(IValidator validator, ILogger<ValidationSchemaTransformer>? logger)
    {
        if (validator is not IEnumerable<IValidationRule> rules)
            yield break;

        foreach (var rule in rules)
        {
            if (!rule.HasNoCondition() || rule is not IIncludeRule)
                continue;

            foreach (var component in rule.Components)
            {
                var validatorType = component.Validator.GetType();

                if (!validatorType.IsGenericType || validatorType.GetGenericTypeDefinition() != typeof(ChildValidatorAdaptor<,>))
                    continue;

                if (TryGetIncludedValidator(component.Validator, logger, out var includeValidator))
                    yield return includeValidator;
            }
        }
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ChildValidatorAdaptor<,>))]
    public void ApplyRulesFromIncludedValidators(OpenApiSchema schema,
                                                 IValidator validator,
                                                 string propertyPrefix,
                                                 HashSet<Type> activeChildValidators,
                                                 System.Text.Json.JsonNamingPolicy? namingPolicy,
                                                 HashSet<Type> activeIncludedValidators)
    {
        var validatorType = validator.GetType();

        if (!activeIncludedValidators.Add(validatorType))
            return;

        try
        {
            foreach (var includeValidator in GetIncludedValidators(validator, logger))
            {
                var includeValidatorType = includeValidator.GetType();

                if (activeIncludedValidators.Contains(includeValidatorType))
                    continue;

                var rulesDict = includeValidator.GetDictionaryOfRules(namingPolicy, usePropertyNamingPolicy, GetValidatorTargetType(includeValidator));
                applyRulesToSchema(schema, new(rulesDict), propertyPrefix, activeChildValidators);
                ApplyRulesFromIncludedValidators(schema, includeValidator, propertyPrefix, activeChildValidators, namingPolicy, activeIncludedValidators);
            }
        }
        finally
        {
            activeIncludedValidators.Remove(validatorType);
        }
    }

    static bool TryGetIncludedValidator(IPropertyValidator adapter, ILogger<ValidationSchemaTransformer>? logger, [NotNullWhen(true)] out IValidator? validator)
    {
        validator = null;
        var adapterType = adapter.GetType();

        try
        {
            var accessor = ChildValidatorAdapterAccessor.Get(adapterType);

            if (!accessor.IsAvailable)
                return false;

            var validationContext = accessor.CreateValidationContext(null);

            if (accessor.GetValidator(adapter, validationContext) is not IValidator resolvedValidator)
                return false;

            validator = resolvedValidator;

            return true;
        }
        catch (Exception ex)
        {
            logger?.ExceptionProcessingValidator(ex, adapterType.Name);

            return false;
        }
    }

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
                    sharedCtx,
                    operationKey,
                    $"{schemaKey}.{propertyName}",
                    localized => schema.Properties![propertyName] = localized);

                if (childSchema is not null)
                {
                    if (childSchema.Type.HasValue &&
                        childSchema.Type.Value.HasFlag(JsonSchemaType.Array) &&
                        ResolveForMutation(
                            childSchema.Items,
                            localizeReferencedSchemas,
                            sharedCtx,
                            operationKey,
                            $"{schemaKey}.{propertyName}[]",
                            localized => childSchema.Items = localized) is { } itemsSchema)
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
