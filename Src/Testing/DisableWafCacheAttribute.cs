namespace FastEndpoints.Testing;

/// <summary>
/// the default behavior of AppFixtures is to never boot up more than one SUT/WAF no matter how many test-classes are using an AppFixture.
/// any derived <see cref="AppFixture{TProgram}" /> that is decorated with this attribute will result in the internal SUT/WAF not being cached and will be instantiated per
/// each test-class in the test project.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class DisableWafCacheAttribute : Attribute;