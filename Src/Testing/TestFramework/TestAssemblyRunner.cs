using System.Globalization;
using System.Reflection;
using Xunit.Internal;
using Xunit.Sdk;
using Xunit.v3;

namespace FastEndpoints.Testing;

sealed class TestAssemblyRunner : XunitTestAssemblyRunner
{
    public new static TestAssemblyRunner Instance { get; } = new();

    static readonly TestCaseOrderer _testCaseOrderer = new();
    static readonly TestCollectionOrderer _testCollectionOrderer = new();

    protected override async ValueTask<bool> OnTestAssemblyStarting(XunitTestAssemblyRunnerContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var result = await base.OnTestAssemblyStarting(ctx);

        await ctx.Aggregator.RunAsync(
            async () =>
            {
                var assemblyFixtureTypes = ctx.TestAssembly
                                              .Assembly
                                              .GetTypes()
                                              .Select(type => type.GetInterfaces())
                                              .SelectMany(tIfc => tIfc)
                                              .Where(tIfc => tIfc.IsAssignableToGenericType(typeof(IAssemblyFixture<>)))
                                              .Select(tIfc => tIfc.GetGenericArguments()[0])
                                              .ToArray();

                await ctx.AssemblyFixtureMappings.InitializeAsync(assemblyFixtureTypes);
            });

        return result;
    }

    protected override List<(IXunitTestCollection Collection, List<IXunitTestCase> TestCases)> OrderTestCollections(XunitTestAssemblyRunnerContext ctx)
    {
        ArgumentNullException.ThrowIfNull(ctx);

        var testCasesByCollection = ctx.TestCases
                                       .GroupBy(tc => tc.TestCollection, TestCollectionComparer<IXunitTestCollection>.Instance)
                                       .ToDictionary(g => g.Key, g => g.ToList());

        IReadOnlyCollection<IXunitTestCollection> orderedTestCollections;

        try
        {
            orderedTestCollections = _testCollectionOrderer.OrderTestCollections(testCasesByCollection.Keys);
        }
        catch (Exception ex)
        {
            var innerEx = ex.Unwrap();

            ctx.MessageBus.QueueMessage(
                new ErrorMessage
                {
                    ExceptionParentIndices = [-1],
                    ExceptionTypes = [typeof(TestPipelineException).SafeName()],
                    Messages =
                    [
                        string.Format(
                            CultureInfo.CurrentCulture,
                            "Assembly-level test collection orderer '{0}' threw '{1}' during ordering: {2}",
                            _testCollectionOrderer.GetType().SafeName(),
                            innerEx.GetType().SafeName(),
                            innerEx.Message)
                    ],
                    StackTraces = [innerEx.StackTrace]
                });

            return [];
        }

        return
            orderedTestCollections
                .Select(collection => (collection, testCasesByCollection[collection]))
                .ToList();
    }

    protected override ValueTask<RunSummary> RunTestCollection(XunitTestAssemblyRunnerContext ctx,
                                                               IXunitTestCollection collection,
                                                               IReadOnlyCollection<IXunitTestCase> cases)

    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(cases);

        return ctx.RunTestCollection(collection, OrderAccordingToClassLevelPriority(cases), _testCaseOrderer);

        static IReadOnlyCollection<IXunitTestCase> OrderAccordingToClassLevelPriority(IReadOnlyCollection<IXunitTestCase> cases)
        {
            var orderedTests = new List<(int priority, IXunitTestCase testCase)>();
            var unorderedTests = new List<IXunitTestCase>();

            foreach (var t in cases)
            {
                var priority = t.TestMethod.TestClass.Class.GetCustomAttribute<PriorityAttribute>()?.Priority;

                if (priority is not null)
                    orderedTests.Add((priority.Value, t));
                else
                    unorderedTests.Add(t);
            }

            return orderedTests.OrderBy(t => t.priority)
                               .Select(t => t.testCase)
                               .Union(unorderedTests)
                               .ToArray();
        }
    }
}