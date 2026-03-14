using System.Collections.Concurrent;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static readonly ConcurrentDictionary<string, byte> _reportedDiagnostics = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> _diagnostics = [];

    internal static void ReportDiagnostic(string message)
    {
        if (_reportedDiagnostics.TryAdd(message, 0))
            _diagnostics.Enqueue(message);
    }

    internal static List<string> DrainDiagnostics()
    {
        var messages = new List<string>();

        while (_diagnostics.TryDequeue(out var message))
        {
            messages.Add(message);
            _reportedDiagnostics.TryRemove(message, out _);
        }

        return messages;
    }

    private static void FlushDiagnostics()
    {
        foreach (var message in DrainDiagnostics())
            Console.WriteLine(message);
    }
}