using FastEndpoints.Agents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints.A2A;

/// <summary>builds the agent card from opt-in FastEndpoints endpoints and <see cref="A2AOptions" />.</summary>
sealed class AgentCardBuilder
{
    readonly IServiceProvider _services;
    readonly A2AOptions _options;
    readonly ILogger<AgentCardBuilder> _logger;

    public AgentCardBuilder(IServiceProvider services, A2AOptions options, ILogger<AgentCardBuilder> logger)
    {
        _services = services;
        _options = options;
        _logger = logger;
    }

    public AgentCard Build(HttpContext ctx)
    {
        var endpointData = _services.GetRequiredService<EndpointData>();
        var skills = new List<AgentSkill>();

        foreach (var def in endpointData.Found)
        {
            var info = def.ResolveSkillInfo();
            if (info is null)
                continue;
            if (_options.SkillFilter is not null && !_options.SkillFilter(def))
                continue;

            var summaryTitle = def.EndpointSummary?.Summary;
            var id = info.Id
                     ?? (!string.IsNullOrWhiteSpace(summaryTitle) ? NamingHelpers.ToSnakeCase(summaryTitle) : null)
                     ?? NamingHelpers.ToSnakeCase(def.EndpointType.Name);

            if (info.Id is null && string.IsNullOrWhiteSpace(summaryTitle))
                _logger.LogWarning(
                    "A2A skill for {EndpointType} has no explicit id and no OpenAPI Summary set. " +
                    "Falling back to type name \"{Id}\". " +
                    "Call Summary(s => s.Summary = ...) or pass an explicit id: this.A2ASkill(\"{Id}\").",
                    def.EndpointType.Name,
                    id);

            var description = info.Description ?? def.EndpointSummary?.Description;

            if (info.Description is null && string.IsNullOrWhiteSpace(def.EndpointSummary?.Description))
                _logger.LogWarning(
                    "A2A skill \"{Id}\" ({EndpointType}) has no description. " +
                    "Call Summary(s => s.Description = ...) or pass an explicit description via this.A2ASkill(configure: info => info.Description = \"...\").",
                    id,
                    def.EndpointType.Name);

            skills.Add(new AgentSkill
            {
                Id = id,
                Name = info.Name ?? summaryTitle ?? id,
                Description = description,
                Tags = info.Tags,
                Examples = info.Examples,
                InputModes = info.InputModes,
                OutputModes = info.OutputModes
            });
        }

        return new AgentCard
        {
            Name = _options.AgentName,
            Description = _options.Description,
            Version = _options.Version,
            Url = _options.Url ?? $"{ctx.Request.Scheme}://{ctx.Request.Host}",
            Provider = _options.Provider,
            Skills = skills
        };
    }
}
