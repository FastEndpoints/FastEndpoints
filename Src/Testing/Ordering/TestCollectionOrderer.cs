using System.Reflection;
using Xunit.Sdk;
using Xunit.v3;

namespace FastEndpoints.Testing;

/// <summary>
/// the default test-collection orderer used to prioritize the execution order of test-collections
/// </summary>
sealed class TestCollectionOrderer : ITestCollectionOrderer
{
    public IReadOnlyCollection<TTestCollection> OrderTestCollections<TTestCollection>(IReadOnlyCollection<TTestCollection> collections)
        where TTestCollection : ITestCollection
    {
        var orderedCollections = new List<(int priority, TTestCollection collection)>();
        var unorderedCollections = new List<TTestCollection>();

        foreach (var c in collections)
        {
            var priority = ((IXunitTestCollection)c).CollectionDefinition?.GetCustomAttribute<PriorityAttribute>()?.Priority;

            if (priority is not null)
                orderedCollections.Add((priority.Value, c));
            else
                unorderedCollections.Add(c);
        }

        return orderedCollections.OrderBy(t => t.priority)
                                 .Select(t => t.collection)
                                 .Union(unorderedCollections)
                                 .ToArray();
    }
}