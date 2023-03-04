using System.Diagnostics;

namespace TestCases.ProcessorStateTest;

public class Thingy
{
    private readonly Stopwatch _stopWatch;

    public int Id { get; set; }
    public string? Name { get; set; }
    public long Duration => _stopWatch.ElapsedMilliseconds;

    public Thingy()
    {
        _stopWatch = new();
        _stopWatch.Start();
    }
}
