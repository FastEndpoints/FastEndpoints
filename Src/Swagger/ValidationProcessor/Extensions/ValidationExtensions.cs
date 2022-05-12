// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
//         of this software and associated documentation files (the "Software"), to deal
//         in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
//         furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
//         copies or substantial portions of the Software.
//
//         THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//         IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//                                                                 FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//         AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//         LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using FluentValidation;
using FluentValidation.Validators;

namespace FastEndpoints.Swagger.ValidationProcessor.Extensions;

/// <summary>
/// Extensions for some swagger specific work.
/// </summary>
internal static class ValidationExtensions
{
    /// <summary>
    /// Is supported swagger numeric type.
    /// </summary>
    public static bool IsNumeric(this object value) => value is int or long or float or double or decimal;

    /// <summary>
    /// Returns not null enumeration.
    /// </summary>
    public static IEnumerable<TValue> NotNull<TValue>(this IEnumerable<TValue>? collection) => collection ?? Array.Empty<TValue>();

    /// <summary>
    /// Returns validation rules by property name ignoring name case.
    /// </summary>
    /// <param name="validator">Validator</param>
    /// <param name="name">Property name.</param>
    /// <returns>enumeration.</returns>
    public static IEnumerable<ValidationRuleContext> GetValidationRulesForMemberIgnoreCase(this IValidator validator, string name) =>
            (validator as IEnumerable<IValidationRule>)
            .NotNull()
            .GetPropertyRules()
            .Where(validationRuleContext => HasNoCondition(validationRuleContext.ValidationRule) &&
                                            validationRuleContext.ValidationRule.PropertyName.EqualsIgnoreAll(name));

    /// <summary>
    /// Returns property validators by property name ignoring name case.
    /// </summary>
    /// <param name="validator">Validator</param>
    /// <param name="name">Property name.</param>
    /// <returns>enumeration.</returns>
    public static IEnumerable<IPropertyValidator> GetValidatorsForMemberIgnoreCase(this IValidator validator, string name) =>
            GetValidationRulesForMemberIgnoreCase(validator, name)
                    .SelectMany(
                                validationRuleContext =>
                                {
                                    return validationRuleContext.ValidationRule.Components.Select(c => c.Validator);
                                });

    /// <summary>
    /// Returns all IValidationRules that are PropertyRule.
    /// If rule is CollectionPropertyRule then isCollectionRule set to true.
    /// </summary>
    internal static IEnumerable<ValidationRuleContext> GetPropertyRules(
            this IEnumerable<IValidationRule> validationRules)
    {
        foreach (var validationRule in validationRules)
        {
            var isCollectionRule = validationRule.GetType() == typeof(ICollectionRule<,>);
            yield return new ValidationRuleContext(validationRule, isCollectionRule);
        }
    }

    // /// <summary>
    // /// Returns a <see cref="bool"/> indicating if the <paramref name="propertyValidator"/> is conditional.
    // /// </summary>
    // internal static bool HasNoCondition(this IPropertyValidator propertyValidator)
    // {
    //     return propertyValidator?.Options?.Condition == null && propertyValidator?.Options?.AsyncCondition == null;
    // }

    /// <summary>
    /// Returns a <see cref="bool"/> indicating if the <paramref name="propertyRule"/> is conditional.
    /// </summary>
    internal static bool HasNoCondition(this IValidationRule propertyRule) => !propertyRule.HasCondition && !propertyRule.HasAsyncCondition;

    /// <summary>
    /// Contains <see cref="IValidationRule"/> and additional info.
    /// </summary>
    public readonly struct ValidationRuleContext
    {
        /// <summary>
        /// PropertyRule.
        /// </summary>
        public readonly IValidationRule ValidationRule;

        /// <summary>
        /// Flag indication whether the <see cref="IValidationRule"/> is the CollectionRule.
        /// </summary>
        public readonly bool IsCollectionRule;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationRuleContext"/> struct.
        /// </summary>
        /// <param name="validationRule">PropertyRule.</param>
        /// <param name="isCollectionRule">Is a CollectionPropertyRule.</param>
        public ValidationRuleContext(IValidationRule validationRule, bool isCollectionRule)
        {
            ValidationRule = validationRule;
            IsCollectionRule = isCollectionRule;
        }
    }
}