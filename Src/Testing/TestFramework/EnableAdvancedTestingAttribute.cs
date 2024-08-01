using Xunit.Sdk;

namespace FastEndpoints.Testing;

/// <summary>
/// this assembly level attribute allows you to order tests at all levels (assembly,collection,class) using the <see cref="PriorityAttribute" /> as well as enable support
/// for assembly fixtures via the <see cref="TestBaseWithAssemblyFixture{TAppFixture}" /> class.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly), TestFrameworkDiscoverer("Xunit.Sdk.TestFrameworkTypeDiscoverer", "xunit.execution.{Platform}")]
public sealed class EnableAdvancedTestingAttribute : Attribute, ITestFrameworkAttribute
{
    public EnableAdvancedTestingAttribute(string _ = TestFramework.TypeName, string __ = TestFramework.AssemblyName) { }
}