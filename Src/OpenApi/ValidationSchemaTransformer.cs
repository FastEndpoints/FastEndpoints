// Original: https://github.com/zymlabs/nswag-fluentvalidation
// MIT License
// Copyright (c) 2019 Zym Labs LLC

using FastEndpoints.OpenApi.ValidationProcessor;
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

        using var scope = _serviceResolver.CreateScope();
        var schemaApplier = new ValidationSchemaApplier(sharedCtx, _serviceResolver, _logger, scope.ServiceProvider, ValidationRuleCatalog.DefaultRules);

        foreach (var binding in bindings)
        {
            try
            {
                var validator = _serviceResolver.CreateInstance(binding.ValidatorType, scope.ServiceProvider) ??
                                throw new InvalidOperationException($"Unable to instantiate validator {binding.ValidatorType.Name}!");
                schemaApplier.ApplyValidator(schema, (IValidator)validator, binding.PropertyPrefix ?? string.Empty, []);
            }
            catch (Exception ex)
            {
                _logger?.ExceptionProcessingValidator(ex, binding.ValidatorType.Name);
            }
        }

        return Task.CompletedTask;
    }
}
