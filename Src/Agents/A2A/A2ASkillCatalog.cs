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

    public EndpointDefinition? FindVisibleSkill(string? id, HttpContext context)
    {
        var skills = GetVisibleSkills(context);

        EnsureUniqueIds(skills, "A2A skills visible to the current caller");

        EndpointDefinition? onlyVisible = null;
        var visibleCount = 0;

        foreach (var skill in skills)
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

    public static void EnsureUniqueIds(IReadOnlyCollection<A2ASkillDescriptor> skills, string scope)
    {
        var collisions = skills.GroupBy(x => x.Id, StringComparer.Ordinal)
                               .Where(g => g.Count() > 1)
                               .ToArray();

        if (collisions.Length == 0)
            return;

        throw new InvalidOperationException(
            "Duplicate A2A skill ids detected among " +
            scope +
            ": " +
            string.Join(
                "; ",
                collisions.Select(
                    g => $"'{g.Key}' => {FormatEndpointTypeNames(g)}")) +
            ". A2A skill ids must be unique.");
    }

    static string FormatEndpointTypeNames(IEnumerable<A2ASkillDescriptor> skills)
        => string.Join(", ", skills.Select(x => x.Definition.EndpointType.FullName ?? x.Definition.EndpointType.Name).Distinct(StringComparer.Ordinal));

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
        var id = info.Id ??
                 (!string.IsNullOrWhiteSpace(summaryTitle) ? NamingHelpers.ToSnakeCase(summaryTitle) : null) ??
                 NamingHelpers.ToSnakeCase(def.EndpointType.Name);

        return new(def, info, id, summaryTitle);
    }
}

sealed record A2ASkillDescriptor(EndpointDefinition Definition, A2ASkillInfo Info, string Id, string? SummaryTitle);
