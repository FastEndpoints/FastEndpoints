using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FastEndpoints.Testing;

class TestAssemblyRunner(ITestAssembly testAssembly,
                         IEnumerable<IXunitTestCase> testCases,
                         IMessageSink diagnosticMessageSink,
                         IMessageSink executionMessageSink,
                         ITestFrameworkExecutionOptions executionOptions)
    : XunitTestAssemblyRunner(testAssembly, testCases, diagnosticMessageSink, executionMessageSink, executionOptions)
{
    readonly Dictionary<Type, object> _assemblyFixtureMappings = new();

    protected override async Task AfterTestAssemblyStartingAsync()
    {
        // Let everything initialize
        await base.AfterTestAssemblyStartingAsync();

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
                   testCases,
                   DiagnosticMessageSink,
                   messageBus,
                   TestCaseOrderer,
                   new(Aggregator),
                   cancellationTokenSource)
               .RunAsync();
}