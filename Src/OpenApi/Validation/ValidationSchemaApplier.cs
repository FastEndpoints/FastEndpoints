using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using FastEndpoints.OpenApi.ValidationProcessor;
using FastEndpoints.OpenApi.ValidationProcessor.Extensions;
using FluentValidation;
using FluentValidation.Validators;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi;
using static FastEndpoints.OpenApi.ValidationSchemaTransformer;

namespace FastEndpoints.OpenApi;

sealed class ValidationSchemaApplier : IDisposable
{
    readonly SharedContext _sharedCtx;
    readonly ILogger<ValidationSchemaTransformer>? _logger;
    readonly FluentValidationRule[] _rules;
    readonly ChildValidatorResolver _childResolver;
    readonly bool _localizeReferencedSchemas;
    readonly bool _usePropertyNamingPolicy;
    readonly string _operationKey;
    readonly string _schemaKey;

    public ValidationSchemaApplier(SharedContext sharedCtx,
                                   IServiceResolver serviceResolver,
                                   ILogger<ValidationSchemaTransformer>? logger,
                                   Func<IServiceScope> createScope,
                                   FluentValidationRule[] rules,
                                   bool usePropertyNamingPolicy,
                                   string operationKey,
                                   string schemaKey,
                                   bool localizeReferencedSchemas = false)
    {
        _sharedCtx = sharedCtx;
        _logger = logger;
        _rules = rules;
        _usePropertyNamingPolicy = usePropertyNamingPolicy;
        _operationKey = operationKey;
        _schemaKey = schemaKey;
        _localizeReferencedSchemas = localizeReferencedSchemas;
        _childResolver = new(
            serviceResolver,
            sharedCtx,
            logger,
            createScope,
            ApplyValidator,
            ApplyRulesToSchema,
            usePropertyNamingPolicy,
            operationKey,
            schemaKey,
            localizeReferencedSchemas);
    }

    public void Dispose()
        => _childResolver.Dispose();

    public void ApplyValidatorRules(OpenApiSchema schema, CachedValidatorRules cachedRules, string propertyPrefix, HashSet<Type> activeChildValidators)
    {
        ApplyRulesToSchema(schema, new(cachedRules.Rules), propertyPrefix, activeChildValidators);

        foreach (var includedRules in cachedRules.IncludedRules)
            ApplyValidatorRules(schema, includedRules, propertyPrefix, activeChildValidators);
    }

    void ApplyValidator(OpenApiSchema schema, IValidator validator, string propertyPrefix, HashSet<Type> activeChildValidators)
    {
        var rulesDict = validator.GetDictionaryOfRules(_sharedCtx.NamingPolicy, _usePropertyNamingPolicy, GetValidatorTargetType(validator));
        ApplyRulesToSchema(schema, new(rulesDict), propertyPrefix, activeChildValidators);
        _childResolver.ApplyRulesFromIncludedValidators(schema, validator, propertyPrefix, activeChildValidators, _sharedCtx.NamingPolicy, []);
    }

    [DynamicDependency(DynamicallyAccessedMemberTypes.PublicMethods, typeof(ChildValidatorAdaptor<,>))]
    public static IEnumerable<IValidator> GetIncludedValidators(IValidator validator, ILogger<ValidationSchemaTransformer>? logger)
        => ChildValidatorResolver.GetIncludedValidators(validator, logger);

    void ApplyRulesToSchema(OpenApiSchema? schema, ValidationRuleLookup rules, string propertyPrefix, HashSet<Type> activeChildValidators)
    {
        if (schema is null)
            return;

        TryApplyRootValidation(schema, rules, propertyPrefix, activeChildValidators);

        if (schema.Properties is not null)
        {
            foreach (var (propertyName, propertySchema) in schema.Properties.ToArray())
                TryApplyValidation(schema, rules, propertyName, propertySchema, propertyPrefix, activeChildValidators);
        }

        RecurseComposite(schema.AllOf, rules, propertyPrefix, activeChildValidators);
        RecurseComposite(schema.OneOf, rules, propertyPrefix, activeChildValidators);
        RecurseComposite(schema.AnyOf, rules, propertyPrefix, activeChildValidators);
    }

    void RecurseComposite(IList<IOpenApiSchema>? composite,
                          ValidationRuleLookup rules,
                          string propertyPrefix,
                          HashSet<Type> activeChildValidators)
    {
        if (composite is null)
            return;

        for (var i = 0; i < composite.Count; i++)
        {
            var resolved = ResolveForMutation(
                composite[i],
                _localizeReferencedSchemas,
                _sharedCtx,
                _operationKey,
                $"{_schemaKey}.{propertyPrefix}.composite[{i}]",
                localized => composite[i] = localized);

            if (resolved?.Properties is { Count: > 0 })
                ApplyRulesToSchema(resolved, rules, propertyPrefix, activeChildValidators);
        }
    }

    void TryApplyValidation(OpenApiSchema schema,
                            ValidationRuleLookup rules,
                            string propertyName,
                            IOpenApiSchema? property,
                            string parameterPrefix,
                            HashSet<Type> activeChildValidators)
    {
        var fullPropertyName = $"{parameterPrefix}{propertyName}";

        if (rules.TryGetValue(fullPropertyName, out var validationRules))
        {
            var localizedPropertySchema = property is null
                                              ? null
                                              : ResolveForMutation(
                                                  property,
                                                  _localizeReferencedSchemas,
                                                  _sharedCtx,
                                                  _operationKey,
                                                  $"{_schemaKey}.{fullPropertyName}",
                                                  localized => schema.Properties![propertyName] = localized);

            foreach (var validationRule in validationRules)
                ApplyValidationRule(schema, validationRule, propertyName, localizedPropertySchema, activeChildValidators);
        }

        if (property is null)
            return;

        var hasNestedObjectRules = rules.HasPrefix($"{fullPropertyName}.");
        var hasNestedArrayRules = rules.HasPrefix($"{fullPropertyName}[].");

        if (!hasNestedObjectRules && !hasNestedArrayRules)
            return;

        var propertySchema = ResolveForMutation(
            property,
            _localizeReferencedSchemas,
            _sharedCtx,
            _operationKey,
            $"{_schemaKey}.{fullPropertyName}",
            localized => schema.Properties![propertyName] = localized);

        if (hasNestedObjectRules &&
            propertySchema is not null &&
            propertySchema.Properties is { Count: > 0 } &&
            propertySchema != schema)
        {
            ApplyRulesToSchema(propertySchema, rules, $"{fullPropertyName}.", activeChildValidators);

            return;
        }

        if (hasNestedArrayRules &&
            propertySchema is { Type: { } type } &&
            type.HasFlag(JsonSchemaType.Array) &&
            ResolveForMutation(
                propertySchema.Items,
                _localizeReferencedSchemas,
                _sharedCtx,
                _operationKey,
                $"{_schemaKey}.{fullPropertyName}[]",
                localized => propertySchema.Items = localized) is { Properties.Count: > 0 } itemsSchema)
            ApplyRulesToSchema(itemsSchema, rules, $"{fullPropertyName}[].", activeChildValidators);
    }

    void TryApplyRootValidation(OpenApiSchema schema,
                                ValidationRuleLookup rules,
                                string propertyPrefix,
                                HashSet<Type> activeChildValidators)
    {
        if (propertyPrefix.Length == 0 ||
            !rules.TryGetValue(propertyPrefix.TrimEnd('.'), out var validationRules))
            return;

        foreach (var validationRule in validationRules)
        {
            foreach (var ruleComponent in validationRule.Components)
            {
                var propertyValidator = ruleComponent.Validator;

                if (propertyValidator.Name == "ChildValidatorAdaptor")
                    _childResolver.ApplyChildValidator(schema, propertyValidator, activeChildValidators);
            }
        }
    }

    void ApplyValidationRule(OpenApiSchema schema,
                             IValidationRule validationRule,
                             string propertyName,
                             OpenApiSchema? propertySchema,
                             HashSet<Type> activeChildValidators)
    {
        var ruleHasCondition = !validationRule.HasNoCondition();

        foreach (var ruleComponent in validationRule.Components)
        {
            var propertyValidator = ruleComponent.Validator;
            var hasCondition = ruleHasCondition || ruleComponent.HasCondition();

            if (propertyValidator.Name == "ChildValidatorAdaptor")
            {
                _childResolver.ApplyChildValidator(schema, propertyName, propertyValidator, activeChildValidators);

                continue;
            }

            foreach (var rule in _rules)
            {
                if (!rule.Matches(propertyValidator))
                    continue;

                try
                {
                    rule.Apply(new(schema, propertyName, propertyValidator, hasCondition, propertySchema));
                }
                catch (Exception ex)
                {
                    _logger?.FailedToApplyValidationRule(ex, propertyName, propertyValidator.Name);
                }
            }
        }
    }

    internal readonly struct ValidationRuleLookup(ReadOnlyDictionary<string, List<IValidationRule>> rules)
    {
        readonly HashSet<string> _prefixes = BuildPrefixes(rules.Keys);

        public bool TryGetValue(string propertyName, [NotNullWhen(true)] out List<IValidationRule>? validationRules)
            => rules.TryGetValue(propertyName, out validationRules);

        public bool HasPrefix(string prefix)
            => _prefixes.Contains(prefix);

        static HashSet<string> BuildPrefixes(IEnumerable<string> ruleNames)
        {
            var prefixes = new HashSet<string>(StringComparer.Ordinal);

            foreach (var ruleName in ruleNames)
            {
                for (var separatorIndex = ruleName.IndexOf('.');
                     separatorIndex >= 0;
                     separatorIndex = ruleName.IndexOf('.', separatorIndex + 1))
                    prefixes.Add(ruleName[..(separatorIndex + 1)]);
            }

            return prefixes;
        }
    }
}
