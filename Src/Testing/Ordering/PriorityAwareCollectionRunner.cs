using Xunit.v3;

namespace FastEndpoints.Testing;

sealed class PriorityAwareCollectionRunner : XunitTestCollectionRunner
{
    public new static PriorityAwareCollectionRunner Instance { get; } = new();

    PriorityAwareCollectionRunner() { }

    protected override async ValueTask<RunSummary> RunTestClasses(XunitTestCollectionRunnerContext ctx, Exception? exception)
    {
        var summary = new RunSummary();

        // GroupBy preserves the order of first key occurrence. since test cases are already sorted by class priority, the groups will appear in priority order.
        // we intentionally do not call .OrderBy(TestClassComparer) which would re-sort by UniqueID hash.
        var groups = ctx.TestCases.GroupBy(tc => tc.TestClass, TestClassComparer.Instance);

        foreach (var testCasesByClass in groups)
        {
            var testClass = testCasesByClass.Key as IXunitTestClass;
            var testCases = testCasesByClass.ToArray();

            if (exception is not null)
                summary.Aggregate(await FailTestClass(ctx, testClass, testCases, exception));
            else
                summary.Aggregate(await RunTestClass(ctx, testClass, testCases));

            if (ctx.CancellationTokenSource.IsCancellationRequested)
                break;
        }

        return summary;
    }
}