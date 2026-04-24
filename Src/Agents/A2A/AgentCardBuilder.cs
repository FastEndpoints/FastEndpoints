using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.A2A;

/// <summary>builds the agent card from opt-in FastEndpoints endpoints and <see cref="A2AOptions" />.</summary>
sealed class AgentCardBuilder
{
    readonly IServiceProvider _services;
    readonly A2AOptions _options;

    public AgentCardBuilder(IServiceProvider services, A2AOptions options)
    {
        _services = services;
        _options = options;
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

            var id = info.Id ?? def.EndpointType.Name;

            skills.Add(new AgentSkill
            {
                Id = id,
                Name = info.Name ?? id,
                Description = info.Description ?? def.EndpointSummary?.Description,
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
