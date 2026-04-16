// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using FluentValidation.Validators;

namespace FastEndpoints.OpenApi.ValidationProcessor;

[HideFromDocs]
#pragma warning disable CS9113 // Parameter is unread.
public class FluentValidationRule(string name)
#pragma warning restore CS9113 // Parameter is unread.
{
    /// <summary>
    /// Predicate to match property validator.
    /// </summary>
    public Func<IPropertyValidator, bool> Matches { get; init; } = _ => false;

    /// <summary>
    /// Modify schema action.
    /// </summary>
    public Action<RuleContext> Apply { get; init; } = _ => { };
}
