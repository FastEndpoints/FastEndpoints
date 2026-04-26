// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using System.Collections.Concurrent;
using System.Text.Json;
using FastEndpoints.OpenApi.ValidationProcessor;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class ValidationSchemaTransformer(DocumentOptions docOpts, SharedContext sharedCtx)
{
    IServiceResolver? _serviceResolver;
    ILogger<ValidationSchemaTransformer>? _logger;
    JsonNamingPolicy? _namingPolicy;
    readonly ConcurrentDictionary<ValidatorRuleCacheKey, Lazy<CachedValidatorRules?>> _validatorRulesCache = new();
    readonly object _initializeLock = new();
    volatile bool _initialized;

    static FluentValidationRule[] CreateDefaultRules()
        => [.. ValidationRuleCatalog.DefaultRules];

    void Initialize(IServiceProvider services)
    {
        if (_initialized)
            return;

        lock (_initializeLock)
        {
            if (_initialized)
                return;

            _namingPolicy = sharedCtx.ResolveNamingPolicy(services);
            _serviceResolver = services.GetRequiredService<IServiceResolver>();
            _logger = services.GetRequiredService<ILogger<ValidationSchemaTransformer>>();

            _initialized = true;
        }
    }

    public void ApplyEndpointValidation(OpenApiOperation operation, IServiceProvider services, Type? validatorType, string? propertyPrefix = null)
    {
        if (validatorType is null || operation.RequestBody?.Content is null)
            return;

        Initialize(services);

        if (_serviceResolver is null)
            return;

        var cachedRules = GetOrCreateValidatorRules(validatorType, _namingPolicy);

        if (cachedRules is null)
            return;

        using var schemaApplier = new ValidationSchemaApplier(
            sharedCtx,
            _serviceResolver,
            _logger,
            _serviceResolver.CreateScope,
            ValidationRuleCatalog.DefaultRules,
            docOpts.UsePropertyNamingPolicy,
            localizeReferencedSchemas: true);

        foreach (var content in operation.RequestBody.Content.Values)
        {
            var schema = content.EnsureOperationLocalSchemaIfShared(sharedCtx);

            if (schema is not null)
                schemaApplier.ApplyValidatorRules(schema, cachedRules, FormatPropertyPrefix(propertyPrefix), []);
        }
    }

    static string FormatPropertyPrefix(string? propertyPrefix)
        => string.IsNullOrWhiteSpace(propertyPrefix) ? string.Empty : $"{propertyPrefix}.";

    CachedValidatorRules? GetOrCreateValidatorRules(Type validatorType, JsonNamingPolicy? namingPolicy)
        => _validatorRulesCache.GetOrAdd(
                                    new(validatorType, namingPolicy),
                                    key => new(
                                        () =>
                                        {
                                            try
                                            {
                                                using var scope = _serviceResolver!.CreateScope();
                                                var validator = _serviceResolver.CreateInstance(key.ValidatorType, scope.ServiceProvider) ??
                                                                throw new InvalidOperationException($"Unable to instantiate validator {key.ValidatorType.Name}!");

                                                return CacheValidatorRules((IValidator)validator, key.NamingPolicy);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger?.ExceptionProcessingValidator(ex, key.ValidatorType.Name);

                                                return null;
                                            }
                                        },
                                        LazyThreadSafetyMode.ExecutionAndPublication))
                                .Value;

    CachedValidatorRules CacheValidatorRules(IValidator validator, JsonNamingPolicy? namingPolicy)
    {
        var rules = validator.GetDictionaryOfRules(namingPolicy, docOpts.UsePropertyNamingPolicy, GetValidatorTargetType(validator));
        var includedRules = ValidationSchemaApplier.GetIncludedValidators(validator, _logger)
                                               .Select(v => CacheValidatorRules(v, namingPolicy))
                                               .ToArray();

        return new(rules, includedRules);
    }

    readonly record struct ValidatorRuleCacheKey(Type ValidatorType, JsonNamingPolicy? NamingPolicy);
}
