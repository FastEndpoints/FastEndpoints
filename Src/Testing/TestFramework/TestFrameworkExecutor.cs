using Xunit.Sdk;
using Xunit.v3;

namespace FastEndpoints.Testing;

sealed class TestFrameworkExecutor(IXunitTestAssembly testAssembly) : XunitTestFrameworkExecutor(testAssembly)
{
    public override async ValueTask RunTestCases(IReadOnlyCollection<IXunitTestCase> testCases,
                                                 IMessageSink executionMessageSink,
                                                 ITestFrameworkExecutionOptions executionOptions,
                                                 CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(executionOptions);
        executionOptions.SetValue(TestOptionsNames.Execution.ParallelMode, nameof(ParallelMode.None));

        await TestAssemblyRunner.Instance.Run(TestAssembly, testCases, executionMessageSink, executionOptions, cancellationToken);
    }
}