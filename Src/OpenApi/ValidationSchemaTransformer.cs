// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using System.Collections.Concurrent;
using FastEndpoints.OpenApi.ValidationProcessor;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed partial class ValidationSchemaTransformer(SharedContext sharedCtx) : IOpenApiSchemaTransformer
{
    IServiceResolver? _serviceResolver;
    ILogger<ValidationSchemaTransformer>? _logger;
    Dictionary<Type, ValidatorBinding[]>? _validatorBindings;
    readonly ConcurrentDictionary<Type, Lazy<CachedValidatorRules?>> _validatorRulesCache = new();
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

            _serviceResolver = services.GetRequiredService<IServiceResolver>();
            _logger = services.GetRequiredService<ILogger<ValidationSchemaTransformer>>();
            var endpointData = _serviceResolver.Resolve<EndpointData>();
            _validatorBindings = ValidatorBindingProvider.Build(endpointData, sharedCtx.NamingPolicy);

            if (_validatorBindings.Count == 0)
                _logger.NoValidatorsFound();

            _initialized = true;
        }
    }

    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        sharedCtx.ResolveNamingPolicy(context.ApplicationServices);
        Initialize(context.ApplicationServices);

        if (_serviceResolver is null || _validatorBindings is not { Count: > 0 })
            return Task.CompletedTask;

        var tRequest = context.JsonTypeInfo.Type;

        if (!_validatorBindings.TryGetValue(tRequest, out var bindings) || bindings.Length == 0)
            return Task.CompletedTask;

        using var schemaApplier = new ValidationSchemaApplier(sharedCtx, _serviceResolver, _logger, _serviceResolver.CreateScope, ValidationRuleCatalog.DefaultRules);

        foreach (var binding in bindings)
        {
            var cachedRules = GetOrCreateValidatorRules(binding.ValidatorType);

            if (cachedRules is not null)
                schemaApplier.ApplyValidatorRules(schema, cachedRules, binding.PropertyPrefix ?? string.Empty, []);
        }

        return Task.CompletedTask;
    }

    CachedValidatorRules? GetOrCreateValidatorRules(Type validatorType)
        => _validatorRulesCache.GetOrAdd(
                                    validatorType,
                                    type => new(
                                        () =>
                                        {
                                            try
                                            {
                                                using var scope = _serviceResolver!.CreateScope();
                                                var validator = _serviceResolver.CreateInstance(type, scope.ServiceProvider) ??
                                                                throw new InvalidOperationException($"Unable to instantiate validator {type.Name}!");

                                                return CacheValidatorRules((IValidator)validator);
                                            }
                                            catch (Exception ex)
                                            {
                                                _logger?.ExceptionProcessingValidator(ex, type.Name);

                                                return null;
                                            }
                                        },
                                        LazyThreadSafetyMode.ExecutionAndPublication))
                                .Value;

    CachedValidatorRules CacheValidatorRules(IValidator validator)
    {
        var rules = validator.GetDictionaryOfRules(sharedCtx.NamingPolicy, GetValidatorTargetType(validator));
        var includedRules = ValidationSchemaApplier.GetIncludedValidators(validator, _logger)
                                               .Select(CacheValidatorRules)
                                               .ToArray();

        return new(rules, includedRules);
    }
}
