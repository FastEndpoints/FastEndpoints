using FastEndpoints.Agents;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints.A2A;

sealed class A2ASkillCatalog(IServiceProvider services, A2AOptions options)
{
    readonly Lazy<AgentEndpointCatalog<A2ASkillDescriptor>> _skillCatalog = new(() => BuildSkillCatalog(services, options));

    public IReadOnlyList<A2ASkillDescriptor> GetVisibleSkills(HttpContext context)
        => _skillCatalog.Value.GetVisible(context.User, context, options.SkillVisibilityFilter);

    public A2ASkillDescriptor? FindVisibleSkill(string? id, HttpContext context)
    {
        var skills = GetVisibleSkills(context);

        _skillCatalog.Value.EnsureUnique(skills, "A2A skills visible to the current caller");

        if (id is not null)
            return _skillCatalog.Value.ResolveVisible(id, context.User, context, options.SkillVisibilityFilter, "A2A skills visible to the current caller", "A2A skill id");

        A2ASkillDescriptor? onlyVisible = null;
        var visibleCount = 0;

        foreach (var skill in skills)
        {
            onlyVisible = skill;
            visibleCount++;

            if (visibleCount > 1)
                return null;
        }

        return visibleCount == 1 ? onlyVisible : null;
    }

    public void EnsureUniqueIds(IReadOnlyCollection<A2ASkillDescriptor> skills, string scope)
        => _skillCatalog.Value.EnsureUnique(skills, scope);

    static AgentEndpointCatalog<A2ASkillDescriptor> BuildSkillCatalog(IServiceProvider services, A2AOptions options)
        => AgentEndpointCatalog<A2ASkillDescriptor>.FromEndpoints(
            services,
            def =>
            {
                var info = def.ResolveSkillInfo();

                if (info is null)
                    return null;

                if (options.SkillFilter is not null && !options.SkillFilter(def))
                    return null;

                return new(def, info, AgentPublishedNameResolver.Resolve(def, info.Id, "A2A skill id", "A2A skill ids"), def.EndpointSummary?.Summary);
            },
            x => x.Id,
            x => x.Definition,
            "A2A skill ids");
}

sealed record A2ASkillDescriptor(EndpointDefinition Definition, A2ASkillInfo Info, string Id, string? SummaryTitle);
