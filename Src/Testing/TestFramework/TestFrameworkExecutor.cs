using Xunit.Sdk;
using Xunit.v3;

namespace FastEndpoints.Testing;

sealed class TestFrameworkExecutor(IXunitTestAssembly testAssembly) : XunitTestFrameworkExecutor(testAssembly)
{
    public override async ValueTask RunTestCases(IReadOnlyCollection<IXunitTestCase> testCases,
                                                 IMessageSink executionMessageSink,
                                                 ITestFrameworkExecutionOptions executionOptions)
        => await TestAssemblyRunner.Instance.Run(TestAssembly, testCases, executionMessageSink, executionOptions);
}