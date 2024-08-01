using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.Testing;

/// <summary>
/// the default test-collection orderer used to prioritize the execution order of test-collections
/// </summary>
sealed class TestCollectionOrderer : ITestCollectionOrderer
{
    public IEnumerable<ITestCollection> OrderTestCollections(IEnumerable<ITestCollection> collections)
    {
        var orderedCollections = new List<(int priority, ITestCollection collection)>();
        var unorderedCollections = new List<ITestCollection>();

        foreach (var c in collections)
        {
            var priority = ((c.CollectionDefinition as IReflectionTypeInfo)?.Type.GetCustomAttribute(typeof(PriorityAttribute)) as PriorityAttribute)?.Priority;

            if (priority is not null)
                orderedCollections.Add((priority.Value, c));
            else
                unorderedCollections.Add(c);
        }

        foreach (var c in orderedCollections.OrderBy(t => t.priority))
            yield return c.collection;

        foreach (var c in unorderedCollections)
            yield return c;
    }
}