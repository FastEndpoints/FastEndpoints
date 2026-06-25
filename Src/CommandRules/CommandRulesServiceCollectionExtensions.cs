using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FastEndpoints;

/// <summary>
/// extension methods for registering command rule services.
/// </summary>
public static class CommandRulesServiceCollectionExtensions
{
    /// <param name="services">service collection.</param>
    extension(IServiceCollection services)
    {
        /// <summary>
        /// adds command rule services with default options.
        /// </summary>
        public IServiceCollection AddCommandRules()
            => services.AddCommandRules(null);

        /// <summary>
        /// adds command rule services with configured options.
        /// </summary>
        /// <param name="configure">configuration action for command rule options.</param>
        public IServiceCollection AddCommandRules(Action<CommandRulesOptions>? configure)
        {
            if (configure is null)
                services.TryAddSingleton<CommandRulesOptions>();
            else
            {
                services.RemoveAll<CommandRulesOptions>();
                services.AddSingleton(
                    _ =>
                    {
                        var options = new CommandRulesOptions();
                        configure(options);

                        return options;
                    });
            }

            services.TryAddScoped(typeof(ICommandRuleEngine<>), typeof(DefaultCommandRuleEngine<>));
            services.TryAddScoped(typeof(ICommandDispatcher<>), typeof(DefaultCommandDispatcher<>));

            return services;
        }

        /// <summary>
        /// registers a command rule for the specified input type.
        /// </summary>
        /// <typeparam name="TInput">the input type handled by the rule.</typeparam>
        /// <typeparam name="TRule">the rule implementation type.</typeparam>
        public IServiceCollection AddCommandRule<TInput, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TRule>()
            where TRule : class, ICommandRule<TInput>
        {
            services.AddCommandRules();
            services.TryAddEnumerable(ServiceDescriptor.Transient<ICommandRule<TInput>, TRule>());

            return services;
        }
    }
}