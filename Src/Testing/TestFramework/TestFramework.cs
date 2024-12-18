using System.Reflection;
using Xunit.v3;

namespace FastEndpoints.Testing;

sealed class TestFramework : XunitTestFramework
{
    protected override ITestFrameworkExecutor CreateExecutor(Assembly assembly)
        => new TestFrameworkExecutor(new XunitTestAssembly(assembly));
}