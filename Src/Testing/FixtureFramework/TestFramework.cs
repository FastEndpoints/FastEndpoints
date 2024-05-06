using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace FastEndpoints.Testing;

public class TestFramework(IMessageSink messageSink) : XunitTestFramework(messageSink)
{
    public const string TypeName = $"FastEndpoints.Testing.{nameof(TestFramework)}";
    public const string AssemblyName = "FastEndpoints.Testing";

    protected override ITestFrameworkExecutor CreateExecutor(AssemblyName assemblyName)
        => new TestFrameworkExecutor(assemblyName, SourceInformationProvider, DiagnosticMessageSink);
}