using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FastEndpoints.Testing;

class TestCollectionRunner(Dictionary<Type, object> assemblyFixtureMappings,
                           ITestCollection testCollection,
                           IEnumerable<IXunitTestCase> testCases,
                           IMessageSink diagnosticMessageSink,
                           IMessageBus messageBus,
                           ITestCaseOrderer testCaseOrderer,
                           ExceptionAggregator aggregator,
                           CancellationTokenSource cancellationTokenSource)
    : XunitTestCollectionRunner(testCollection, testCases, diagnosticMessageSink, messageBus, testCaseOrderer, aggregator, cancellationTokenSource)
{
    readonly IMessageSink _diagnosticMessageSink = diagnosticMessageSink;

    protected override Task<RunSummary> RunTestClassAsync(ITestClass testClass, IReflectionTypeInfo @class, IEnumerable<IXunitTestCase> testCases)
    {
        foreach (var fixtureType in @class.Type.GetTypeInfo().ImplementedInterfaces
                                          .Where(i => i.GetTypeInfo().IsGenericType && i.GetGenericTypeDefinition() == typeof(IAssemblyFixture<>))
                                          .Select(i => i.GetTypeInfo().GenericTypeArguments.Single())
                                          .Where(i => !assemblyFixtureMappings.ContainsKey(i)))
        {
            // ConcurrentDictionary's GetOrAdd does not lock around the value factory call, so we need
            // to do it ourselves.
            lock (assemblyFixtureMappings)
            {
                if (!assemblyFixtureMappings.ContainsKey(fixtureType))
                    Aggregator.Run(() => assemblyFixtureMappings.Add(fixtureType, CreateAssemblyFixtureInstance(fixtureType)));
            }
        }

        // Don't want to use .Concat + .ToDictionary because of the possibility of overriding types,
        // so instead we'll just let collection fixtures override assembly fixtures.
        var combinedFixtures = new Dictionary<Type, object>(assemblyFixtureMappings);
        foreach (var kvp in CollectionFixtureMappings)
            combinedFixtures[kvp.Key] = kvp.Value;

        // We've done everything we need, so let the built-in types do the rest of the heavy lifting
        return new XunitTestClassRunner(
            testClass,
            @class,
            testCases,
            _diagnosticMessageSink,
            MessageBus,
            TestCaseOrderer,
            new(Aggregator),
            CancellationTokenSource,
            combinedFixtures).RunAsync();
    }

    object CreateAssemblyFixtureInstance(Type fixtureType)
    {
        var constructors = fixtureType.GetConstructors();

        if (constructors.Length > 1)
            throw new($"The type ${fixtureType.FullName} can only contain one constructor.");

        return constructors[0].GetParameters().Length == 0
                   ? Activator.CreateInstance(fixtureType)!
                   : Activator.CreateInstance(fixtureType, _diagnosticMessageSink)!;
    }
}