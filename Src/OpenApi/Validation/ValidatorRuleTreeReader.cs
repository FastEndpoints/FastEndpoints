// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using FluentValidation.Internal;
using FluentValidation.Validators;
using Microsoft.Extensions.Logging;

namespace FastEndpoints.OpenApi;

static class ValidatorRuleTreeReader
{
    static Type GetValidatorTargetType(IValidator validator)
        => validator.GetType().GetGenericArgumentsOfType(Types.ValidatorOf1)?[0] ?? validator.GetType();

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ChildValidatorAdaptor<,>))]
    static IEnumerable<IValidator> GetIncludedValidators(IValidator validator, ILogger<ValidationSchemaTransformer>? logger)
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

    public static CachedValidatorRules Read(IValidator validator,
                                            JsonNamingPolicy? namingPolicy,
                                            bool usePropertyNamingPolicy,
                                            ILogger<ValidationSchemaTransformer>? logger)
        => Read(validator, namingPolicy, usePropertyNamingPolicy, logger, []);

    static CachedValidatorRules Read(IValidator validator,
                                     JsonNamingPolicy? namingPolicy,
                                     bool usePropertyNamingPolicy,
                                     ILogger<ValidationSchemaTransformer>? logger,
                                     HashSet<Type> activeIncludedValidators)
    {
        var rules = validator.GetDictionaryOfRules(namingPolicy, usePropertyNamingPolicy, GetValidatorTargetType(validator));
        var validatorType = validator.GetType();

        if (!activeIncludedValidators.Add(validatorType))
            return new(rules, []);

        try
        {
            var includedRules = new List<CachedValidatorRules>();

            foreach (var includedValidator in GetIncludedValidators(validator, logger))
            {
                if (activeIncludedValidators.Contains(includedValidator.GetType()))
                    continue;

                includedRules.Add(Read(includedValidator, namingPolicy, usePropertyNamingPolicy, logger, activeIncludedValidators));
            }

            return new(rules, [.. includedRules]);
        }
        finally
        {
            activeIncludedValidators.Remove(validatorType);
        }
    }
}
