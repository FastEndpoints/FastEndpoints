using System.Reflection;
using FastEndpoints.OpenApi;

namespace OpenApi;

public class XmlDocLookupTests
{
    [Fact]
    public void assembly_without_location_reads_xml_from_app_directory()
    {
        // an assembly loaded from bytes has no location, like one bundled into a single-file app
        var assembly = Assembly.Load(File.ReadAllBytes(typeof(XmlDocLookupTests).Assembly.Location));
        var type = assembly.GetType(typeof(DocumentedType).FullName!)!;

        assembly.Location.ShouldBeEmpty();
        XmlDocLookup.GetTypeSummary(type).ShouldBe("A documented type.");
        XmlDocLookup.GetPropertySummary(type.GetProperty(nameof(DocumentedType.Name))!).ShouldBe("The name property.");
    }

    /// <summary>A documented type.</summary>
    public sealed class DocumentedType
    {
        /// <summary>The name property.</summary>
        public string Name { get; set; } = "";
    }
}
