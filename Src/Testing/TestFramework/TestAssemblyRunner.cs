using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FastEndpoints.Testing;

sealed class TestAssemblyRunner(ITestAssembly testAssembly,
                                IEnumerable<IXunitTestCase> testCases,
                                IMessageSink diagnosticMessageSink,
                                IMessageSink executionMessageSink,
                                ITestFrameworkExecutionOptions executionOptions)
    : XunitTestAssemblyRunner(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
{
    static readonly TestCaseOrderer _testCaseOrderer = new();
    static readonly TestCollectionOrderer _testCollectionOrderer = new();
    readonly Dictionary<Type, object> _assemblyFixtureMappings = new();

    protected override async Task AfterTestAssemblyStartingAsync()
    {
        // Let everything initialize
        await base.AfterTestAssemblyStartingAsync();
        TestCollectionOrderer = _testCollectionOrderer;

        // Go find all the AssemblyFixtureAttributes adorned on the test assembly
        await Aggregator.RunAsync(
            async () =>
            {
                var assemblyFixtures = new HashSet<Type>(
                    ((IReflectionAssemblyInfo)TestAssembly.Assembly)
                    .Assembly
                    .GetTypes()
                    .Select(type => type.GetInterfaces())
                    .SelectMany(x => x)
                    .Where(@interface => @interface.IsAssignableToGenericType(typeof(IAssemblyFixture<>)))
                    .ToArray());

                // Instantiate all the fixtures
                foreach (var fixtureAttribute in assemblyFixtures)
                {
                    var fixtureType = fixtureAttribute.GetGenericArguments()[0];
                    var hasConstructorWithMessageSink = fixtureType.GetConstructor([typeof(IMessageSink)]) != null;
                    _assemblyFixtureMappings[fixtureType] = hasConstructorWithMessageSink
                                                                ? Activator.CreateInstance(fixtureType, ExecutionMessageSink)!
                                                                : Activator.CreateInstance(fixtureType)!;
                }

                // Initialize IAsyncLifetime fixtures
                foreach (var asyncLifetime in _assemblyFixtureMappings.Values.OfType<IAsyncLifetime>())
                    await Aggregator.RunAsync(async () => await asyncLifetime.InitializeAsync());
            });
    }

    protected override async Task BeforeTestAssemblyFinishedAsync()
    {
        // Make sure we clean up everybody who is disposable, and use Aggregator.Run to isolate Dispose failures
        Parallel.ForEach(
            _assemblyFixtureMappings.Values.OfType<IDisposable>(),
            disposable => Aggregator.Run(disposable.Dispose));

        await Parallel.ForEachAsync(
            _assemblyFixtureMappings.Values.OfType<IAsyncDisposable>(),
            async (disposable, _) => await Aggregator.RunAsync(async () => await disposable.DisposeAsync()));

        await Parallel.ForEachAsync(
            _assemblyFixtureMappings.Values.OfType<IAsyncLifetime>(),
            async (disposable, _) => await Aggregator.RunAsync(async () => await disposable.DisposeAsync()));

        await base.BeforeTestAssemblyFinishedAsync();
    }

    protected override async Task<RunSummary> RunTestCollectionAsync(IMessageBus messageBus,
                                                                     ITestCollection testCollection,
                                                                     IEnumerable<IXunitTestCase> testCases,
                                                                     CancellationTokenSource cancellationTokenSource)
        => await new TestCollectionRunner(
                   _assemblyFixtureMappings,
                   testCollection,
                   OrderTestsInCollection(testCases),
                   DiagnosticMessageSink,
                   messageBus,
                   _testCaseOrderer,
                   new(Aggregator),
                   cancellationTokenSource)
               .RunAsync();

    static IEnumerable<IXunitTestCase> OrderTestsInCollection(IEnumerable<IXunitTestCase> tests)
    {
        var orderedTests = new List<(int priority, IXunitTestCase testCase)>();
        var unorderedTests = new List<IXunitTestCase>();

        foreach (var t in tests)
        {
            var priority = t.TestMethod.TestClass.Class
                            .GetCustomAttributes(typeof(PriorityAttribute))?
                            .SingleOrDefault()?
                            .GetNamedArgument<int>(nameof(PriorityAttribute.Priority));

            if (priority is not null)
                orderedTests.Add((priority.Value, t));
            else
                unorderedTests.Add(t);
        }

        foreach (var t in orderedTests.OrderBy(t => t.priority))
            yield return t.testCase;

        foreach (var t in unorderedTests)
            yield return t;
    }
}