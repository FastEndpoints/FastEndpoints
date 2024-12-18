using System.Reflection;
using Xunit.v3;
using Xunit.Sdk;

namespace FastEndpoints.Testing;

sealed class TestFrameworkExecutor(IXunitTestAssembly testAssembly) :
    XunitTestFrameworkExecutor(testAssembly)
{
    // protected override async void RunTestCases(IEnumerable<IXunitTestCase> testCases, IMessageSink executionMessageSink, ITestFrameworkExecutionOptions executionOptions)
    // {
    //     using var assemblyRunner = new TestAssemblyRunner(TestAssembly, testCases, DiagnosticMessageSink, executionMessageSink, executionOptions);
    //     await assemblyRunner.RunAsync();
    // }

    public override async ValueTask RunTestCases(IReadOnlyCollection<IXunitTestCase> testCases,
                                                 IMessageSink executionMessageSink,
                                                 ITestFrameworkExecutionOptions executionOptions) { }
}