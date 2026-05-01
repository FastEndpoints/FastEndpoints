using System.Collections.Concurrent;
using System.Text.Json.Nodes;
using FluentValidation;
using FluentValidation.Internal;
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
    {
        if (schema is not JsonObject root)
            return;

        var rules = GetRules(validator);
        if (rules.Length == 0)
            return;

        var props = root["properties"] as JsonObject;
        var required = root["required"] as JsonArray;

        foreach (var (propertyName, propValidator) in rules)
        {
            if (props is null || props[propertyName] is not JsonObject propSchema)
                continue;

            switch (propValidator)
            {
                case INotNullValidator or INotEmptyValidator:
                    required ??= [];
                    var alreadyRequired = false;
                    foreach (var r in required)
                    {
                        if (r?.GetValue<string>() == propertyName)
                        {
                            alreadyRequired = true;
                            break;
                        }
                    }
                    if (!alreadyRequired)
                        required.Add(propertyName);
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

        if (required is not null && required.Count > 0 && root["required"] is null)
            root["required"] = required;
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
}
