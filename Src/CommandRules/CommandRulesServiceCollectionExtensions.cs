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
        /// adds command rule services with configured rules and options.
        /// </summary>
        /// <param name="configure">configuration action for command rules.</param>
        public IServiceCollection AddCommandRules(Action<CommandRulesBuilder> configure)
        {
            ArgumentNullException.ThrowIfNull(configure);

            var options = new CommandRulesOptions();
            configure(new(services, options));

            services.RemoveAll<CommandRulesOptions>();
            services.AddSingleton(options);
            services.TryAddScoped(typeof(ICommandRuleEngine<>), typeof(DefaultCommandRuleEngine<>));
            services.TryAddScoped(typeof(ICommandDispatcher<>), typeof(DefaultCommandDispatcher<>));

            return services;
        }
    }
}
