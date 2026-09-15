// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using System.Collections.Concurrent;
using System.Text.Json;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;

namespace FastEndpoints.OpenApi;

sealed class ValidationSchemaTransformer(DocumentOptions docOpts, SharedContext sharedCtx)
{
    IServiceResolver? _serviceResolver;
    ILogger<ValidationSchemaTransformer>? _logger;
    JsonNamingPolicy? _namingPolicy;
    readonly ConcurrentDictionary<ValidatorRuleCacheKey, Lazy<CachedValidatorRules?>> _validatorRulesCache = new();
    readonly Lock _initializeLock = new();
    volatile bool _initialized;

    void Initialize(IServiceProvider services)
    {
        if (_initialized)
            return;

        lock (_initializeLock)
        {
            if (_initialized)
                return;

            _namingPolicy = sharedCtx.ResolveNamingPolicy();
            _serviceResolver = services.GetRequiredService<IServiceResolver>();
            _logger = services.GetRequiredService<ILogger<ValidationSchemaTransformer>>();

            _initialized = true;
        }
    }

    public void ApplyEndpointValidation(OpenApiOperation operation,
                                        IServiceProvider services,
                                        Type? validatorType,
                                        string operationKey,
                                        RequestTransformState parameterState,
                                        OpenApiGenerationState generation,
                                        string? propertyPrefix = null)
    {
        if (validatorType is null)
            return;

        var hasRequestBody = operation.RequestBody?.Content is { Count: > 0 };
        var hasParameters = parameterState.ParametersBySchemaPath.Count > 0;

        if (!hasRequestBody && !hasParameters)
            return;

        Initialize(services);

        if (_serviceResolver is null)
            return;

        var cachedRules = GetOrCreateValidatorRules(validatorType, _namingPolicy);

        if (cachedRules is null)
            return;

        if (hasRequestBody)
            ApplyRequestBodyValidation(operation, cachedRules, operationKey, generation, propertyPrefix);

        if (hasParameters)
            ApplyParameterValidation(parameterState, cachedRules, operationKey, generation);
    }

    void ApplyRequestBodyValidation(OpenApiOperation operation, CachedValidatorRules cachedRules, string operationKey, OpenApiGenerationState generation, string? propertyPrefix)
    {
        using var schemaApplier = CreateSchemaApplier(operationKey, "requestBody", generation);
        var formattedPropertyPrefix = FormatPropertyPrefix(propertyPrefix);

        foreach (var content in operation.RequestBody!.Content!.Values)
        {
            var schema = content.EnsureOperationLocalSchemaForMutation(sharedCtx, generation, operationKey, "requestBody");

            if (schema is not null)
                schemaApplier.ApplyValidatorRules(schema, cachedRules, formattedPropertyPrefix, []);
        }
    }

    void ApplyParameterValidation(RequestTransformState parameterState, CachedValidatorRules cachedRules, string operationKey, OpenApiGenerationState generation)
    {
        var properties = new Dictionary<string, IOpenApiSchema>(parameterState.ParametersBySchemaPath.Count, StringComparer.Ordinal);
        var paramByKey = new Dictionary<string, OpenApiParameter>(parameterState.ParametersBySchemaPath.Count, StringComparer.Ordinal);
        var schemaByParam = new Dictionary<OpenApiParameter, OpenApiSchema>(parameterState.ParametersBySchemaPath.Count, ReferenceEqualityComparer.Instance);

        foreach (var (schemaPath, param) in parameterState.ParametersBySchemaPath)
        {
            if (!schemaByParam.TryGetValue(param, out var schema))
            {
                schema = GetMutableParameterSchema(param, operationKey, schemaPath, generation);

                if (schema is null)
                    continue;

                schemaByParam[param] = schema;
            }

            properties[schemaPath] = schema;
            paramByKey[schemaPath] = param;
        }

        if (properties.Count == 0)
            return;

        var synthetic = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Properties = properties
        };

        using var schemaApplier = CreateSchemaApplier(operationKey, "parameters", generation);
        schemaApplier.ApplyValidatorRules(synthetic, cachedRules, string.Empty, []);

        if (synthetic.Required is not { Count: > 0 } required)
            return;

        foreach (var name in required)
        {
            if (paramByKey.TryGetValue(name, out var param))
                param.Required = true;
        }
    }

    ValidationSchemaApplier CreateSchemaApplier(string operationKey, string schemaKey, OpenApiGenerationState generation)
        => new(
            sharedCtx,
            generation,
            _serviceResolver!,
            _logger,
            _serviceResolver!.CreateScope,
            ValidationRuleCatalog.DefaultRules,
            docOpts.UsePropertyNamingPolicy,
            operationKey,
            schemaKey,
            localizeReferencedSchemas: true);

    OpenApiSchema? GetMutableParameterSchema(OpenApiParameter param, string operationKey, string schemaPath, OpenApiGenerationState generation)
    {
        var schemaKey = $"parameter.{schemaPath}";

        if (param.Schema is not null)
            return param.Schema.EnsureSchemaForMutation(sharedCtx, generation, operationKey, schemaKey, localized => param.Schema = localized, cloneConcreteSchema: true);

        if (param.Content is not { Count: > 0 })
            return null;

        foreach (var content in param.Content.Values)
        {
            var schema = content.EnsureOperationLocalSchemaForMutation(sharedCtx, generation, operationKey, schemaKey);

            if (schema is not null)
                return schema;
        }

        return null;
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
        => CacheValidatorRules(validator, namingPolicy, []);

    CachedValidatorRules CacheValidatorRules(IValidator validator, JsonNamingPolicy? namingPolicy, HashSet<Type> activeIncludedValidators)
    {
        var rules = validator.GetDictionaryOfRules(namingPolicy, docOpts.UsePropertyNamingPolicy, GetValidatorTargetType(validator));
        var validatorType = validator.GetType();

        if (!activeIncludedValidators.Add(validatorType))
            return new(rules, []);

        try
        {
            var includedRules = new List<CachedValidatorRules>();

            foreach (var includedValidator in ValidationSchemaApplier.GetIncludedValidators(validator, _logger))
            {
                if (activeIncludedValidators.Contains(includedValidator.GetType()))
                    continue;

                includedRules.Add(CacheValidatorRules(includedValidator, namingPolicy, activeIncludedValidators));
            }

            return new(rules, [.. includedRules]);
        }
        finally
        {
            activeIncludedValidators.Remove(validatorType);
        }
    }

    internal static OpenApiSchema? ResolveForMutation(IOpenApiSchema? schema,
                                                      bool localizeReferencedSchemas,
                                                      SharedContext sharedCtx,
                                                      OpenApiGenerationState generation,
                                                      string operationKey,
                                                      string schemaKey,
                                                      Action<IOpenApiSchema> replace)
    {
        if (!localizeReferencedSchemas || schema is not OpenApiSchemaReference)
            return schema.ResolveSchema();

        return schema.EnsureSchemaForMutation(sharedCtx, generation, operationKey, schemaKey, replace);
    }

    internal static Type GetValidatorTargetType(IValidator validator)
        => validator.GetType().GetGenericArgumentsOfType(Types.ValidatorOf1)?[0] ?? validator.GetType();
}

internal sealed record CachedValidatorRules(System.Collections.ObjectModel.ReadOnlyDictionary<string, List<IValidationRule>> Rules, CachedValidatorRules[] IncludedRules);

readonly record struct ValidatorRuleCacheKey(Type ValidatorType, JsonNamingPolicy? NamingPolicy);