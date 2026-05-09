using FastEndpoints.Agents;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace FastEndpoints.A2A;

sealed class A2ASkillCatalog(IServiceProvider services, A2AOptions options)
{
    public IReadOnlyList<A2ASkillDescriptor> GetVisibleSkills(HttpContext context)
    {
        var endpointData = services.GetRequiredService<EndpointData>();
        var skills = new List<A2ASkillDescriptor>();

        foreach (var def in endpointData.Found)
        {
            var skill = CreateDescriptor(def, context);

            if (skill is not null)
                skills.Add(skill);
        }

        return skills;
    }

    public A2ASkillDescriptor? FindVisibleSkill(string? id, HttpContext context)
    {
        var skills = GetVisibleSkills(context);

        EnsureUniqueIds(skills, "A2A skills visible to the current caller");

        A2ASkillDescriptor? onlyVisible = null;
        var visibleCount = 0;

        foreach (var skill in skills)
        {
            if (id is not null)
            {
                if (skill.Id == id)
                    return skill;

                continue;
            }

            onlyVisible = skill;
            visibleCount++;

            if (visibleCount > 1)
                return null;
        }

        return visibleCount == 1 ? onlyVisible : null;
    }

    public static void EnsureUniqueIds(IReadOnlyCollection<A2ASkillDescriptor> skills, string scope)
        => AgentCatalogUniqueness.EnsureUnique(skills, scope, x => x.Id, x => x.Definition, "A2A skill ids");

    A2ASkillDescriptor? CreateDescriptor(EndpointDefinition def, HttpContext context)
    {
        var info = def.ResolveSkillInfo();

        if (info is null)
            return null;

        if (options.SkillFilter is not null && !options.SkillFilter(def))
            return null;

        if (!options.SkillVisibilityFilter(def, context.User, context))
            return null;

        var summaryTitle = def.EndpointSummary?.Summary;
        var id = A2ASkillIdResolver.ResolvePublishedId(def, info);

        return new(def, info, id, summaryTitle);
    }
}

sealed record A2ASkillDescriptor(EndpointDefinition Definition, A2ASkillInfo Info, string Id, string? SummaryTitle);
