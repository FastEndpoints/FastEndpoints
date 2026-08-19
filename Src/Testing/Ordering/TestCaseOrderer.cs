using System.Reflection;
using Xunit.Sdk;
using Xunit.v3;

namespace FastEndpoints.Testing;

sealed class TestCaseOrderer : ITestCaseOrderer
{
    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> tests) where TTestCase : ITestCase
        => PriorityOrdering.OrderByPriority(
            tests,
            t => ((IXunitTestCase)t).TestMethod.Method.GetCustomAttribute<PriorityAttribute>()?.Priority);
}