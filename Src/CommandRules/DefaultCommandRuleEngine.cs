namespace FastEndpoints;

/// <summary>
/// default command engine that evaluates registered rules in order.
/// </summary>
/// <typeparam name="TInput">the input type to evaluate.</typeparam>
public sealed class DefaultCommandRuleEngine<TInput> : ICommandRuleEngine<TInput>
{
    readonly ICommandRule<TInput>[] _rules;
    readonly CommandRulesOptions _options;

    /// <summary>
    /// initializes a new default command engine.
    /// </summary>
    /// <param name="rules">registered rules to evaluate.</param>
    /// <param name="options">command rule options.</param>
    public DefaultCommandRuleEngine(IEnumerable<ICommandRule<TInput>> rules, CommandRulesOptions options)
    {
        _rules = rules.OrderBy(r => r is IOrderedCommandRule ordered ? ordered.Order : 0).ToArray();
        _options = options;
    }

    /// <summary>
    /// evaluates the supplied input to zero or more commands.
    /// </summary>
    /// <param name="input">the input to evaluate.</param>
    /// <param name="ct">cancellation token.</param>
    public async ValueTask<CommandRulePlan> EvaluateAsync(TInput input, CancellationToken ct = default)
    {
        var matchedRuleCount = 0;
        var commands = new List<PlannedCommand>();

        foreach (var rule in _rules)
        {
            var match = await rule.EvaluateAsync(input, ct);
            CommandRuleValidation.ValidateRuleMatch(rule.GetType(), match);

            if (!match.Matched)
                continue;

            matchedRuleCount++;
            commands.AddRange(match.Commands);

            if (_options.MatchMode == CommandRuleMatchMode.First)
                break;
        }

        if (matchedRuleCount == 0)
        {
            return _options.UnhandledBehavior == UnhandledRuleBehavior.Throw
                       ? throw new CommandRuleNotFoundException(typeof(TInput))
                       : CommandRulePlan.Empty;
        }

        return new(matchedRuleCount, commands.ToArray());
    }
}