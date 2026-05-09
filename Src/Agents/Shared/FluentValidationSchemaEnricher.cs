using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Nodes;
using FluentValidation;
using FluentValidation.Validators;

namespace FastEndpoints.Agents;

/// <summary>
/// enriches a JSON-Schema node produced by <see cref="JsonSchemaBuilder" /> with constraints derived
/// from a FluentValidation <see cref="IValidator" />. subset of rules covered: <c>NotNull</c>/<c>NotEmpty</c>
/// (<c>required</c>), <c>Length</c>/<c>MinimumLength</c>/<c>MaximumLength</c>, <c>InclusiveBetween</c>
/// (for strings and numbers), <c>Regex</c>/<c>Matches</c> (<c>pattern</c>), and <c>EmailAddress</c>
/// (<c>format: email</c>). mirrors the rules ported from the <c>FastEndpoints.OpenApi</c>
/// <c>ValidationSchemaTransformer</c> onto <see cref="JsonNode" /> instead of OpenAPI.
/// </summary>
static class FluentValidationSchemaEnricher
{
    static readonly ConcurrentDictionary<Type, (string PropertyName, IPropertyValidator Validator)[]> _ruleCache = new();

    /// <summary>
    /// mutates <paramref name="schema" /> in place by applying every rule in <paramref name="validator" />
    /// that can be expressed in JSON Schema. rules that cannot be expressed (e.g. <c>Must</c> predicates,
    /// cross-property rules) are silently ignored — they still run at request time via the normal
    /// endpoint validation pipeline.
    /// </summary>
    public static void Enrich(JsonNode schema, IValidator validator)
        => Enrich(schema, validator, null, null);

    public static void Enrich(JsonNode schema, IValidator validator, Type? dtoType, JsonSerializerOptions? serializerOptions)
    {
        if (schema is not JsonObject root)
            return;

        var rules = GetRules(validator);

        if (rules.Length == 0)
            return;

        var propertyNames = dtoType is not null && serializerOptions is not null
                                ? AgentJsonPropertyNames.BuildSchemaNameMap(dtoType, serializerOptions)
                                : null;

        foreach (var (propertyName, propValidator) in rules)
        {
            if (!TryResolvePropertySchema(root, propertyName, propertyNames, out var parentSchema, out var schemaPropertyName, out var propSchema))
                continue;

            switch (propValidator)
            {
                case INotNullValidator or INotEmptyValidator:
                    var required = parentSchema["required"] as JsonArray ?? [];
                    var alreadyRequired = false;

                    foreach (var r in required)
                    {
                        if (r?.GetValue<string>() == schemaPropertyName)
                        {
                            alreadyRequired = true;

                            break;
                        }
                    }
                    if (!alreadyRequired)
                        required.Add(schemaPropertyName);
                    if (parentSchema["required"] is null)
                        parentSchema["required"] = required;

                    break;

                case ILengthValidator lenValidator:
                    if (lenValidator.Min > 0)
                        propSchema["minLength"] = lenValidator.Min;
                    if (lenValidator.Max > 0)
                        propSchema["maxLength"] = lenValidator.Max;

                    break;

                case IRegularExpressionValidator rxValidator when !string.IsNullOrWhiteSpace(rxValidator.Expression):
                    propSchema["pattern"] = rxValidator.Expression;

                    break;

                case IEmailValidator:
                    propSchema["format"] = "email";

                    break;

                case IComparisonValidator cmpValidator when cmpValidator.ValueToCompare is IConvertible:
                    ApplyNumericComparison(propSchema, cmpValidator);

                    break;

                case IBetweenValidator btwValidator:
                    propSchema["minimum"] = JsonValue.Create(btwValidator.From);
                    propSchema["maximum"] = JsonValue.Create(btwValidator.To);

                    break;
            }
        }
    }

    static bool TryResolvePropertySchema(JsonObject root,
                                         string validationPropertyName,
                                         IReadOnlyDictionary<string, string>? propertyNames,
                                         out JsonObject parentSchema,
                                         out string schemaPropertyName,
                                         out JsonObject propSchema)
    {
        parentSchema = root;
        schemaPropertyName = string.Empty;
        propSchema = null!;

        foreach (var (segment, isLast) in ParsePropertyPath(validationPropertyName))
        {
            schemaPropertyName = ResolveSchemaPropertyName(segment, propertyNames);

            if (parentSchema["properties"] is not JsonObject props || props[schemaPropertyName] is not JsonObject currentPropSchema)
                return false;

            propSchema = currentPropSchema;

            if (isLast)
                return true;

            if (TryGetNestedObjectSchema(propSchema) is not { } nestedSchema)
                return false;

            parentSchema = nestedSchema;
        }

        return false;
    }

    static JsonObject? TryGetNestedObjectSchema(JsonObject propSchema)
    {
        if (propSchema["properties"] is JsonObject)
            return propSchema;

        return propSchema["items"] is JsonObject { } items && items["properties"] is JsonObject
                   ? items
                   : null;
    }

    static IEnumerable<(string Segment, bool IsLast)> ParsePropertyPath(string propertyPath)
    {
        var segments = new List<string>();
        var start = 0;

        for (var i = 0; i <= propertyPath.Length; i++)
        {
            if (i == propertyPath.Length || propertyPath[i] == '.' || propertyPath[i] == '[')
            {
                if (i > start)
                    segments.Add(propertyPath[start..i]);

                if (i < propertyPath.Length && propertyPath[i] == '[')
                {
                    var end = propertyPath.IndexOf(']', i + 1);
                    i = end < 0 ? propertyPath.Length : end;
                }

                start = i + 1;
            }
        }

        for (var i = 0; i < segments.Count; i++)
            yield return (segments[i], i == segments.Count - 1);
    }

    static void ApplyNumericComparison(JsonObject propSchema, IComparisonValidator cmp)
    {
        var value = JsonValue.Create(cmp.ValueToCompare);

        switch (cmp.Comparison)
        {
            case Comparison.GreaterThan:
                propSchema["exclusiveMinimum"] = value;

                break;
            case Comparison.GreaterThanOrEqual:
                propSchema["minimum"] = value;

                break;
            case Comparison.LessThan:
                propSchema["exclusiveMaximum"] = value;

                break;
            case Comparison.LessThanOrEqual:
                propSchema["maximum"] = value;

                break;
        }
    }

    static (string, IPropertyValidator)[] GetRules(IValidator validator)
        => _ruleCache.GetOrAdd(
            validator.GetType(),
            _ =>
            {
                if (validator is not IEnumerable<IValidationRule> ruleEnum)
                    return [];

                var list = new List<(string, IPropertyValidator)>();

                foreach (var rule in ruleEnum)
                {
                    if (rule.PropertyName is null)
                        continue;

                    foreach (var component in rule.Components)
                        list.Add((rule.PropertyName, component.Validator));
                }

                return list.ToArray();
            });

    static string ResolveSchemaPropertyName(string validationPropertyName, IReadOnlyDictionary<string, string>? propertyNames)
        => propertyNames is not null && propertyNames.TryGetValue(validationPropertyName, out var schemaPropertyName)
               ? schemaPropertyName
               : validationPropertyName;
}
