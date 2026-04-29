// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using FluentValidation;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using FluentValidation.Internal;

namespace FastEndpoints.OpenApi.ValidationProcessor.Extensions;

/// <summary>
/// Extensions for some swagger specific work.
/// </summary>
static class ValidationExtensions
{
    /// <summary>
    /// Is supported swagger numeric type.
    /// </summary>
    internal static bool IsNumeric(this object value)
        => value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    internal static string ToInvariantNumericString(this object value)
        => Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Creates a dictionary with the validation rules.
    /// </summary>
    internal static ReadOnlyDictionary<string, List<IValidationRule>> GetDictionaryOfRules(this IValidator validator, Type? validatorTargetType = null)
        => validator.GetDictionaryOfRules(null, true, validatorTargetType);

    internal static ReadOnlyDictionary<string, List<IValidationRule>> GetDictionaryOfRules(this IValidator validator,
                                                                                           JsonNamingPolicy? namingPolicy,
                                                                                           bool usePropertyNamingPolicy = true,
                                                                                           Type? validatorTargetType = null)
    {
        var rulesDict = new Dictionary<string, List<IValidationRule>>();
        validatorTargetType ??= validator.GetType().GetGenericArgumentsOfType(Types.ValidatorOf1)?[0];

        if (validator is IEnumerable<IValidationRule> rules)
        {
            foreach (var rule in rules.GetPropertyRules())
            {
                var propertyNameRaw = rule.ValidationRule.PropertyName;
                var propertyNameWithSchemaCasing = validatorTargetType is null
                                                       ? propertyNameRaw.ConvertToSchemaCasing(namingPolicy, usePropertyNamingPolicy)
                                                       : PropertyNameResolver.ConvertPropertyPath(validatorTargetType,
                                                                                                  propertyNameRaw,
                                                                                                  namingPolicy,
                                                                                                  usePropertyNamingPolicy);

                if (rulesDict.TryGetValue(propertyNameWithSchemaCasing, out var propertyRules))
                    propertyRules.Add(rule.ValidationRule);
                else
                    rulesDict.Add(propertyNameWithSchemaCasing, [rule.ValidationRule]);
            }
        }

        return new(rulesDict);
    }

    /// <summary>
    /// Converts a property name to the schema casing by using the selected JsonNamingPolicy
    /// </summary>
    internal static string ConvertToSchemaCasing(this string propertyName, JsonNamingPolicy? namingPolicy, bool usePropertyNamingPolicy = true)
    {
        if (namingPolicy is null || !usePropertyNamingPolicy)
            return propertyName;

        var segments = propertyName.Split('.');
        for (var i = 0; i < segments.Length; i++)
            segments[i] = namingPolicy.ConvertName(segments[i]);

        return string.Join(".", segments);
    }

    internal static IEnumerable<ValidationRuleContext> GetPropertyRules(this IEnumerable<IValidationRule> validationRules)
    {
        foreach (var validationRule in validationRules)
        {
            if (validationRule.Member is null || string.IsNullOrEmpty(validationRule.PropertyName))
                continue;

            yield return new(validationRule);
        }
    }

    internal static bool HasNoCondition(this IValidationRule propertyRule)
        => propertyRule is { HasCondition: false, HasAsyncCondition: false };

    internal static bool HasCondition(this IRuleComponent component)
        => component.HasCondition || component.HasAsyncCondition;

    /// <summary>
    /// Contains IValidationRule and additional info.
    /// </summary>
    public readonly struct ValidationRuleContext(IValidationRule validationRule)
    {
        public readonly IValidationRule ValidationRule = validationRule;
    }
}
