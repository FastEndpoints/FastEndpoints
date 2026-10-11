// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using System.Text.Json;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using static FastEndpoints.OpenApi.ValidationSchemaTransformer;

namespace FastEndpoints.OpenApi;

static class ValidatorRuleTreeReader
{
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

            foreach (var includedValidator in ChildValidatorResolver.GetIncludedValidators(validator, logger))
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
