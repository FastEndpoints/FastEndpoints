using Xunit.v3;

namespace FastEndpoints.Testing;

/// <summary>
/// this assembly level attribute allows you to order tests at all levels (assembly,collection,class) using the <see cref="PriorityAttribute" /> as well as
/// enable support
/// for assembly fixtures via the <see cref="TestBaseWithAssemblyFixture{TAppFixture}" /> class.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class EnableAdvancedTestingAttribute : Attribute, ITestFrameworkAttribute
{
    public Type FrameworkType { get; } = typeof(TestFramework);
}