using FastEndpoints.Agents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FastEndpoints.A2A;

/// <summary>builds the agent card from opt-in FastEndpoints endpoints and <see cref="A2AOptions" />.</summary>
sealed class AgentCardBuilder(IServiceProvider services, A2AOptions options, ILogger<AgentCardBuilder> logger)
{
    public AgentCard Build(HttpContext ctx)
    {
        var endpointData = services.GetRequiredService<EndpointData>();
        var skills = new List<AgentSkill>();

        foreach (var def in endpointData.Found)
        {
            var info = def.ResolveSkillInfo();

            if (info is null)
                continue;

            if (options.SkillFilter is not null && !options.SkillFilter(def))
                continue;

            if (options.SkillVisibilityFilter is not null && !options.SkillVisibilityFilter(def, ctx.User, ctx))
                continue;

            var summaryTitle = def.EndpointSummary?.Summary;
            var id = info.Id ?? (!string.IsNullOrWhiteSpace(summaryTitle) ? NamingHelpers.ToSnakeCase(summaryTitle) : null) ?? NamingHelpers.ToSnakeCase(def.EndpointType.Name);

            if (info.Id is null && string.IsNullOrWhiteSpace(summaryTitle))
            {
                logger.LogWarning(
                    "A2A skill for {EndpointType} has no explicit id and no OpenAPI Summary set. " +
                    "Falling back to type name \"{FallbackId}\". " +
                    "Set Summary(s => s.Summary = ...) or pass the id explicitly.",
                    def.EndpointType.Name,
                    id);
            }

            var description = info.Description ?? def.EndpointSummary?.Description;

            if (info.Description is null && string.IsNullOrWhiteSpace(def.EndpointSummary?.Description))
            {
                logger.LogWarning(
                    "A2A skill \"{Id}\" ({EndpointType}) has no description. " +
                    "Call Summary(s => s.Description = ...) or pass an explicit description via this.A2ASkill(configure: info => info.Description = \"...\").",
                    id,
                    def.EndpointType.Name);
            }

            skills.Add(
                new()
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

        return new()
        {
            Name = options.AgentName,
            Description = options.Description,
            Version = options.Version,
            Url = options.Url ?? $"{ctx.Request.Scheme}://{ctx.Request.Host}",
            Provider = options.Provider,
            Skills = skills
        };
    }
}
