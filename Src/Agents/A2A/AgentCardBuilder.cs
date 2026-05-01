using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace FastEndpoints.A2A;

/// <summary>builds the agent card from opt-in FastEndpoints endpoints and <see cref="A2AOptions" />.</summary>
sealed class AgentCardBuilder(A2ASkillCatalog skillCatalog, A2AOptions options, ILogger<AgentCardBuilder> logger)
{
    public AgentCard Build(HttpContext ctx)
    {
        var skills = new List<AgentSkill>();

        foreach (var skill in skillCatalog.GetVisibleSkills(ctx))
        {
            var info = skill.Info;

            if (info.Id is null && string.IsNullOrWhiteSpace(skill.SummaryTitle))
            {
                logger.LogWarning(
                    "A2A skill for {EndpointType} has no explicit id and no OpenAPI Summary set. " +
                    "Falling back to type name \"{FallbackId}\". " +
                    "Set Summary(s => s.Summary = ...) or pass the id explicitly.",
                    skill.Definition.EndpointType.Name,
                    skill.Id);
            }

            var description = info.Description ?? skill.Definition.EndpointSummary?.Description;

            if (info.Description is null && string.IsNullOrWhiteSpace(skill.Definition.EndpointSummary?.Description))
            {
                logger.LogWarning(
                    "A2A skill \"{Id}\" ({EndpointType}) has no description. " +
                    "Call Summary(s => s.Description = ...) or pass an explicit description via this.A2ASkill(configure: info => info.Description = \"...\").",
                    skill.Id,
                    skill.Definition.EndpointType.Name);
            }

            skills.Add(
                new()
                {
                    Id = skill.Id,
                    Name = info.Name ?? skill.SummaryTitle ?? skill.Id,
                    Description = description ?? string.Empty,
                    Tags = info.Tags ?? [],
                    Examples = info.Examples,
                    InputModes = info.InputModes,
                    OutputModes = info.OutputModes
                });
        }

        return new()
        {
            Name = options.AgentName,
            Description = options.Description ?? string.Empty,
            Version = options.Version,
            Provider = options.Provider,
            SupportedInterfaces =
            [
                new()
                {
                    Url = options.Url ?? BuildRpcUrl(ctx),
                    ProtocolBinding = "JSONRPC",
                    ProtocolVersion = "1.0"
                }
            ],
            Skills = skills
        };
    }

    string BuildRpcUrl(HttpContext ctx)
        => $"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.PathBase}{NormalizePath(options.RpcPattern)}";

    static string NormalizePath(string path)
        => string.IsNullOrWhiteSpace(path)
               ? "/a2a"
               : path[0] == '/' ? path : "/" + path;
}
