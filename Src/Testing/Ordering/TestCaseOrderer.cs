using System.Reflection;
using Xunit.Sdk;
using Xunit.v3;

namespace FastEndpoints.Testing;

sealed class TestCaseOrderer : ITestCaseOrderer
{
    public IReadOnlyCollection<TTestCase> OrderTestCases<TTestCase>(IReadOnlyCollection<TTestCase> tests) where TTestCase : ITestCase
    {
        var orderedTests = new List<(int priority, TTestCase test)>();
        var unorderedTests = new List<TTestCase>();

        foreach (var t in tests)
        {
            var priority = ((IXunitTestCase)t).TestMethod.Method.GetCustomAttribute<PriorityAttribute>()?.Priority;

            if (priority is not null)
                orderedTests.Add((priority.Value, t));
            else
                unorderedTests.Add(t);
        }

        return orderedTests.OrderBy(t => t.priority)
                           .Select(t => t.test)
                           .Union(unorderedTests)
                           .ToArray();
    }
}