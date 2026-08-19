using System.Reflection;
using Xunit.Sdk;
using Xunit.v3;

namespace FastEndpoints.Testing;

sealed class TestMethodOrderer : ITestMethodOrderer
{
    public IReadOnlyCollection<TTestMethod?> OrderTestMethods<TTestMethod>(IReadOnlyCollection<TTestMethod?> testMethods)
        where TTestMethod : ITestMethod
        => PriorityOrdering.OrderByPriority(
            testMethods,
            m => m is IXunitTestMethod x
                     ? x.Method.GetCustomAttribute<PriorityAttribute>()?.Priority
                     : null);
}