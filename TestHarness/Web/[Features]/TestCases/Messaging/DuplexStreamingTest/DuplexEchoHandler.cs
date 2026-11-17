using System.Runtime.CompilerServices;

namespace TestCases.DuplexStreamingTest;

public sealed class DuplexEchoHandler : IDuplexStreamCommandHandler<DuplexPing, DuplexPong>
{
    public async IAsyncEnumerable<DuplexPong> ExecuteAsync(IAsyncEnumerable<DuplexPing> stream, [EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var ping in stream.WithCancellation(ct))
        {
            if (ping.Fail)
                throw new InvalidOperationException("duplex handler failure");

            yield return new() { Number = ping.Number };

            if (ping.Stop)
                yield break;
        }
    }
}
