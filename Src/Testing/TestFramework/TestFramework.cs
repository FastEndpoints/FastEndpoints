using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FastEndpoints.Testing;

sealed class TestFramework(IMessageSink messageSink) : XunitTestFramework(messageSink)
{
    internal const string AssemblyName = "FastEndpoints.Testing";
    internal const string TypeName = $"{AssemblyName}.{nameof(TestFramework)}";

    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        => new TestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
}