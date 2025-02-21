using Xunit.Internal;
using Xunit.Sdk;
using Xunit.v3;

namespace FastEndpoints.Testing;

sealed class TestFrameworkExecutor(IXunitTestAssembly testAssembly) : XunitTestFrameworkExecutor(testAssembly)
{
    public override async ValueTask RunTestCases(IReadOnlyCollection<IXunitTestCase> testCases,
                                                 IMessageSink executionMessageSink,
                                                 ITestFrameworkExecutionOptions executionOptions)
    {
        SetDisableParallelization(executionOptions, true);

        await TestAssemblyRunner.Instance.Run(TestAssembly, testCases, executionMessageSink, executionOptions);

        static void SetDisableParallelization(ITestFrameworkExecutionOptions executionOptions, bool? value)
        {
            Guard.ArgumentNotNull(executionOptions);
            executionOptions.SetValue(TestOptionsNames.Execution.DisableParallelization, value);
        }
    }
}