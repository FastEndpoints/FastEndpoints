using System.Collections.Concurrent;

namespace FastEndpoints.Generator.Cli;

/// <summary>
/// collects deduplicated warnings from any thread and prints them in the order they were reported.
/// </summary>
static class Diagnostics
{
    private static readonly ConcurrentDictionary<string, byte> _reported = new(StringComparer.Ordinal);
    private static readonly ConcurrentQueue<string> _messages = [];
    private static readonly AsyncLocal<ConcurrentQueue<string>?> _captured = new();

    internal static void Report(string message)
    {
        if (_captured.Value is { } captured)
        {
            captured.Enqueue(message);

            return;
        }

        if (_reported.TryAdd(message, 0))
            _messages.Enqueue(message);
    }

    /// <summary>
    /// runs <paramref name="work" /> with its warnings held back instead of reported, so the caller can report them only if the result gets used.
    /// call it from the work's own task (ex: inside <c>Task.Run</c>), so the capture doesn't leak into the calling flow.
    /// </summary>
    internal static (T Result, IEnumerable<string> Warnings) Capture<T>(Func<T> work)
    {
        var captured = new ConcurrentQueue<string>();
        _captured.Value = captured;

        try
        {
            return (work(), captured);
        }
        finally
        {
            _captured.Value = null;
        }
    }

    internal static void Flush()
    {
        while (_messages.TryDequeue(out var message))
        {
            Console.WriteLine(message);
            _reported.TryRemove(message, out _);
        }
    }
}
