using Xunit.Abstractions;
using Xunit.Sdk;

namespace FastEndpoints.Testing;

sealed class TestCaseOrderer : ITestCaseOrderer
{
    internal const string Assembly = "FastEndpoints.Testing";
    internal const string Name = $"{Assembly}.{nameof(TestCaseOrderer)}";

    public IEnumerable<TTestCase> OrderTestCases<TTestCase>(IEnumerable<TTestCase> tests) where TTestCase : ITestCase
    {
        var orderedTests = new List<(int priority, TTestCase test)>();
        var unorderedTests = new List<TTestCase>();

        foreach (var t in tests)
        {
            var priority = t.TestMethod.Method
                            .GetCustomAttributes(typeof(PriorityAttribute))
                            .SingleOrDefault()?
                            .GetNamedArgument<int>(nameof(PriorityAttribute.Priority));

            if (priority is not null)
                orderedTests.Add((priority.Value, t));
            else
                unorderedTests.Add(t);
        }

        foreach (var t in orderedTests.OrderBy(t => t.priority))
            yield return t.test;

        foreach (var t in unorderedTests)
            yield return t;
    }
}