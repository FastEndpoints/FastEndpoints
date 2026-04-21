using System.Reflection;
using System.Runtime.CompilerServices;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Xml.Linq;

namespace FastEndpoints.OpenApi;

static class XmlDocLookup
{
    // cache keyed by assembly instance (not assembly.Location) to avoid clashes between
    // dynamic/in-memory assemblies that share an empty Location string.
    // the value is a pre-indexed dictionary of xml member entries keyed by member id (e.g. "P:Namespace.Type.Prop")
    // so per-property lookup is O(1) instead of an O(N) Descendants scan.
    static readonly ConditionalWeakTable<Assembly, Dictionary<string, XElement>> _xmlDocCache = new();
    static readonly Dictionary<string, XElement> _emptyMembers = new();

    internal static string? GetPropertySummary(PropertyInfo prop)
        => GetMemberSummary(prop.DeclaringType?.Assembly, PropertyMemberId(prop));

    internal static string? GetPropertyExample(PropertyInfo prop)
        => GetMemberExample(prop.DeclaringType?.Assembly, PropertyMemberId(prop));

    internal static string? GetTypeSummary(Type type)
        => GetMemberSummary(type.Assembly, $"T:{GetTypeId(type)}");

    static string PropertyMemberId(PropertyInfo prop)
        => $"P:{GetTypeId(prop.DeclaringType)}.{prop.Name}";

    static string? GetMemberSummary(Assembly? assembly, string memberId)
    {
        if (assembly is null)
            return null;

        var members = GetXmlDoc(assembly);

        if (!members.TryGetValue(memberId, out var member))
            return null;

        var summaryEl = member.Element("summary");

        if (summaryEl is null)
            return null;

        var summary = GetTextWithSeeRefs(summaryEl).Trim();

        return string.IsNullOrWhiteSpace(summary) ? null : summary;
    }

    static string GetTextWithSeeRefs(XElement element)
    {
        var sb = new StringBuilder();

        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    sb.Append(text.Value);

                    break;
                case XElement { Name.LocalName: "see" } el:
                {
                    var cref = el.Attribute("cref")?.Value;

                    if (cref is not null)
                    {
                        // extract the short member name from cref (e.g. "P:Namespace.Type.Member" → "Member")
                        var lastDot = cref.LastIndexOf('.');
                        sb.Append(lastDot >= 0 ? cref[(lastDot + 1)..] : cref);
                    }

                    break;
                }
            }
        }

        return sb.ToString();
    }

    static string? GetMemberExample(Assembly? assembly, string memberId)
    {
        if (assembly is null)
            return null;

        var members = GetXmlDoc(assembly);

        if (!members.TryGetValue(memberId, out var member))
            return null;

        var example = member.Element("example")?.Value.Trim();

        return string.IsNullOrWhiteSpace(example) ? null : example;
    }

    static string GetTypeId(Type? type)
    {
        if (type is null)
            return string.Empty;

        var fullName = type.FullName ?? type.Name;

        var genericArgsIdx = fullName.IndexOf('[');
        if (genericArgsIdx >= 0)
            fullName = fullName[..genericArgsIdx];

        // for nested types: Namespace.Outer+Inner → Namespace.Outer.Inner
        return fullName.Replace('+', '.');
    }

    static Dictionary<string, XElement> GetXmlDoc(Assembly assembly)
        => _xmlDocCache.GetValue(assembly, LoadXmlDoc);

    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "XML doc lookup is optional when assembly paths are unavailable.")]
    static Dictionary<string, XElement> LoadXmlDoc(Assembly assembly)
    {
        var location = assembly.Location;

        if (string.IsNullOrEmpty(location))
            return _emptyMembers;

        var xmlPath = Path.ChangeExtension(location, ".xml");

        if (!File.Exists(xmlPath))
            return _emptyMembers;

        XDocument doc;

        try
        {
            doc = XDocument.Load(xmlPath);
        }
        catch
        {
            // malformed XML or IO failure — fall back to an empty index rather than crashing the transform
            return _emptyMembers;
        }

        var index = new Dictionary<string, XElement>(StringComparer.Ordinal);

        foreach (var member in doc.Descendants("member"))
        {
            var name = member.Attribute("name")?.Value;

            if (!string.IsNullOrEmpty(name))
                index[name] = member;
        }

        return index;
    }
}
