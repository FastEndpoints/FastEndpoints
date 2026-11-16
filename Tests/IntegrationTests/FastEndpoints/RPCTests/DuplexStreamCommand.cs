using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Threading.Channels;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using TestCases.DuplexStreamingTest;

namespace RemoteProcedureCalls;

public class DuplexStreamCommand(Sut f) : RpcTestBase(f)
{
    [Fact]
    public async Task Duplex_Stream_Echoes_All_Items()
    {
        var results = new List<int>();

        await Within(
            async () =>
            {
                await foreach (var pong in Pings(5).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                    results.Add(pong.Number);
            });

        results.ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task Duplex_Stream_Sends_And_Receives_Concurrently()
    {
        // each command is only sent after the previous result arrives, which deadlocks unless both directions are open at once.
        var input = Channel.CreateUnbounded<DuplexPing>();
        await input.Writer.WriteAsync(new() { Number = 1 }, Cancellation);
        var results = new List<int>();

        await Within(
            async () =>
            {
                await foreach (var pong in input.Reader.ReadAllAsync(Cancellation).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                {
                    results.Add(pong.Number);

                    if (pong.Number < 5)
                        await input.Writer.WriteAsync(new() { Number = pong.Number + 1 }, Cancellation);
                    else
                        input.Writer.Complete();
                }
            });

        results.ShouldBe([1, 2, 3, 4, 5]);
    }

    [Fact]
    public async Task Duplex_Stream_Completes_When_Server_Ends_Early()
    {
        var results = new List<int>();

        await Within(
            async () =>
            {
                await foreach (var pong in Pings(int.MaxValue, stopAt: 3).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                    results.Add(pong.Number);
            });

        results.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Duplex_Stream_Caller_Can_Stop_Reading_Early()
    {
        var results = new List<int>();

        await Within(
            async () =>
            {
                await foreach (var pong in Pings(int.MaxValue).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                {
                    results.Add(pong.Number);

                    if (pong.Number == 3)
                        break;
                }
            });

        results.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Duplex_Stream_Surfaces_Handler_Exception()
    {
        var results = new List<int>();

        var ex = await Should.ThrowAsync<RpcException>(
                     () => Within(
                         async () =>
                         {
                             await foreach (var pong in Pings(int.MaxValue, failAt: 3).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                                 results.Add(pong.Number);
                         }));

        ex.StatusCode.ShouldBe(StatusCode.Unknown);
        results.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task Duplex_Stream_Surfaces_Command_Stream_Exception()
    {
        var ex = await Should.ThrowAsync<ApplicationException>(
                     () => Within(
                         async () =>
                         {
                             await foreach (var _ in FailingPings().RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation)) { }
                         }));

        ex.Message.ShouldBe("command stream failure");

        static async IAsyncEnumerable<DuplexPing> FailingPings()
        {
            yield return new() { Number = 1 };

            await Task.Yield();

            throw new ApplicationException("command stream failure");
        }
    }

    [Fact]
    public async Task Duplex_Stream_Completes_When_Server_Ends_Early_And_Commands_Ignore_Cancellation()
    {
        var results = new List<int>();

        await Within(
            async () =>
            {
                await foreach (var pong in StubbornPings(3, stopAt: 3).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                    results.Add(pong.Number);
            });

        results.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Duplex_Stream_Caller_Can_Stop_Reading_Early_When_Commands_Ignore_Cancellation()
    {
        var results = new List<int>();

        await Within(
            async () =>
            {
                await foreach (var pong in StubbornPings(3).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                {
                    results.Add(pong.Number);

                    if (pong.Number == 3)
                        break;
                }
            });

        results.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Duplex_Stream_Caller_Cancellation_Ends_The_Call()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        var results = new List<int>();

        var ex = await Should.ThrowAsync<RpcException>(
                     () => Within(
                         async () =>
                         {
                             await foreach (var pong in StubbornPings(2).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(cts.Token))
                             {
                                 results.Add(pong.Number);

                                 if (pong.Number == 2)
                                     await cts.CancelAsync();
                             }
                         }));

        ex.StatusCode.ShouldBe(StatusCode.Cancelled);
        results.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task Duplex_Stream_Surfaces_Command_Serialization_Failure()
    {
        var results = new List<int>();

        var ex = await Should.ThrowAsync<Exception>(
                     () => Within(
                         async () =>
                         {
                             await foreach (var pong in Pings(int.MaxValue, failSerializationAt: 2).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                                 results.Add(pong.Number);
                         }));

        ex.ShouldNotBeOfType<RpcException>();
        ex.ToString().ShouldContain("serialization failure");
        results.ShouldBeSubsetOf([1]); //the fault can surface before the first result is read
        //note: item 2 fails because a write failure before the server's first response is held by grpc-dotnet's retry wrapper until the call commits
    }

    [Fact]
    public async Task Duplex_Stream_Completes_When_Server_Ends_Early_And_Reader_Is_Slow()
    {
        // the sender keeps writing to the ended call while the reader is away. those failed writes must not turn into an error.
        var results = new List<int>();

        await Within(
            async () =>
            {
                await foreach (var pong in Pings(int.MaxValue, stopAt: 3).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                {
                    results.Add(pong.Number);

                    if (pong.Number == 3)
                        await Task.Delay(500, Cancellation);
                }
            });

        results.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task Duplex_Stream_Surfaces_Handler_Exception_When_Reader_Is_Slow()
    {
        var results = new List<int>();

        var ex = await Should.ThrowAsync<RpcException>(
                     () => Within(
                         async () =>
                         {
                             await foreach (var pong in Pings(int.MaxValue, failAt: 3).RemoteExecuteDuplexAsync<DuplexPing, DuplexPong>(Cancellation))
                             {
                                 results.Add(pong.Number);

                                 if (pong.Number == 2)
                                     await Task.Delay(500, Cancellation);
                             }
                         }));

        ex.StatusCode.ShouldBe(StatusCode.Unknown);
        results.ShouldBe([1, 2]);
    }

    [Fact]
    public async Task Duplex_Stream_Surfaces_Oversized_Command()
    {
        // an item over the max send size fails the write with an RpcException but doesn't end the call by itself
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory, LoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        var remote = new RemoteConnection("http://testhost", services.BuildServiceProvider());
        remote.ChannelOptions.HttpHandler = Remote.ChannelOptions.HttpHandler; //same in-memory test server
        remote.ChannelOptions.MaxSendMessageSize = 1024;
        remote.RegisterDuplexStream<DuplexPing, DuplexPong>();
        var results = new List<int>();

        var ex = await Should.ThrowAsync<RpcException>(
                     () => Within(
                         async () =>
                         {
                             await foreach (var pong in remote.ExecuteDuplexStream<DuplexPing, DuplexPong>(Items(), typeof(DuplexPing), default))
                                 results.Add(pong.Number);
                         }));

        ex.StatusCode.ShouldBe(StatusCode.ResourceExhausted);
        results.ShouldBe([1]);

        //the oversized item is only sent after the first result, because a write failure before that is held by grpc-dotnet's retry wrapper
        async IAsyncEnumerable<DuplexPing> Items()
        {
            yield return new() { Number = 1 };

            while (results.Count == 0)
                await Task.Delay(5, Cancellation);

            yield return new() { Number = 2, Payload = Convert.ToHexString(RandomNumberGenerator.GetBytes(4096)) }; //random so compression can't shrink it
        }
    }

    // fails the test instead of hanging it if the call doesn't end
    static Task Within(Func<Task> body)
        => body().WaitAsync(TimeSpan.FromSeconds(10));

    static async IAsyncEnumerable<DuplexPing> Pings(int count,
                                                    int stopAt = 0,
                                                    int failAt = 0,
                                                    int failSerializationAt = 0,
                                                    [EnumeratorCancellation] CancellationToken ct = default)
    {
        for (var i = 1; i <= count; i++)
        {
            yield return new() { Number = i, Stop = i == stopAt, Fail = i == failAt, FailSerialization = i == failSerializationAt };

            await Task.Delay(5, ct);
        }
    }

    // yields the given number of commands and then waits forever without observing cancellation
    static async IAsyncEnumerable<DuplexPing> StubbornPings(int count, int stopAt = 0)
    {
        for (var i = 1; i <= count; i++)
            yield return new() { Number = i, Stop = i == stopAt };

        await new TaskCompletionSource().Task;
    }
}
