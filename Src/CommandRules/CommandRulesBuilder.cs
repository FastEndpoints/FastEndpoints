using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastEndpoints;

/// <summary>
/// builder for configuring command rules.
/// </summary>
public sealed class CommandRulesBuilder(IServiceCollection services, CommandRulesOptions options)
{
    /// <summary>
    /// controls whether evaluation stops after the first match or collects all matches.
    /// </summary>
    public CommandRuleMatchMode MatchMode
    {
        get => options.MatchMode;
        set => options.MatchMode = value;
    }

    /// <summary>
    /// controls behavior when no rule matches an input.
    /// </summary>
    public UnhandledRuleBehavior UnhandledBehavior
    {
        get => options.UnhandledBehavior;
        set => options.UnhandledBehavior = value;
    }

    /// <summary>
    /// dispatch mode used when a planned command does not specify one and no mode is forced by the dispatcher call.
    /// </summary>
    public CommandDispatchMode DefaultMode
    {
        get => options.DefaultMode;
        set => options.DefaultMode = value;
    }

    /// <summary>
    /// controls whether dispatch stops or continues after a command dispatch failure.
    /// </summary>
    public CommandDispatchFailureBehavior FailureBehavior
    {
        get => options.FailureBehavior;
        set => options.FailureBehavior = value;
    }

    /// <summary>
    /// registers a command rule for the specified input type.
    /// </summary>
    /// <typeparam name="TInput">the input type handled by the rule.</typeparam>
    /// <typeparam name="TRule">the rule implementation type.</typeparam>
    public CommandRulesBuilder Register<TInput, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRule>()
        where TRule : class, ICommandRule<TInput>
    {
        services.TryAddEnumerable(ServiceDescriptor.Transient<ICommandRule<TInput>, TRule>());

        return this;
    }
}
