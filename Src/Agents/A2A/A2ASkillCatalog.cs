using FastEndpoints.Agents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.A2A;

sealed class A2ASkillCatalog(IServiceProvider services, A2AOptions options)
{
    public IEnumerable<A2ASkillDescriptor> GetVisibleSkills(HttpContext ctx)
    {
        var endpointData = services.GetRequiredService<EndpointData>();

        foreach (var def in endpointData.Found)
        {
            var skill = CreateDescriptor(def, ctx);

            if (skill is not null)
                yield return skill;
        }
    }

    public EndpointDefinition? FindVisibleSkill(string? id, HttpContext ctx)
    {
        EndpointDefinition? onlyVisible = null;
        var visibleCount = 0;

        foreach (var skill in GetVisibleSkills(ctx))
        {
            if (id is not null)
            {
                if (skill.Id == id)
                    return skill.Definition;

                continue;
            }

            onlyVisible = skill.Definition;
            visibleCount++;

            if (visibleCount > 1)
                return null;
        }

        return visibleCount == 1 ? onlyVisible : null;
    }

    A2ASkillDescriptor? CreateDescriptor(EndpointDefinition def, HttpContext ctx)
    {
        var info = def.ResolveSkillInfo();

        if (info is null)
            return null;

        if (options.SkillFilter is not null && !options.SkillFilter(def))
            return null;

        if (options.SkillVisibilityFilter is not null && !options.SkillVisibilityFilter(def, ctx.User, ctx))
            return null;

        var summaryTitle = def.EndpointSummary?.Summary;
        var id = info.Id ??
                 (!string.IsNullOrWhiteSpace(summaryTitle) ? NamingHelpers.ToSnakeCase(summaryTitle) : null) ??
                 NamingHelpers.ToSnakeCase(def.EndpointType.Name);

        return new(def, info, id, summaryTitle);
    }
}

sealed record A2ASkillDescriptor(EndpointDefinition Definition, A2ASkillInfo Info, string Id, string? SummaryTitle);
