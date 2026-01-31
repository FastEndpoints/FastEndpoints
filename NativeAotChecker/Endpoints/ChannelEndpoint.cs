using FastEndpoints;
using System.Threading.Channels;

namespace NativeAotChecker.Endpoints;

// Request
public class ChannelRequest
{
    public int ItemCount { get; set; }
    public int ChannelCapacity { get; set; }
    public List<string> Items { get; set; } = [];
}

public class ChannelResponse
{
    public int ItemsWritten { get; set; }
    public int ItemsRead { get; set; }
    public List<string> ReadItems { get; set; } = [];
    public bool ChannelWorked { get; set; }
}

/// <summary>
/// Tests System.Threading.Channels in AOT mode.
/// AOT ISSUE: Channel&lt;T&gt; generic instantiation at runtime.
/// BoundedChannelOptions uses generic factory methods.
/// AsyncEnumerable from channel needs state machine generation.
/// </summary>
public class ChannelEndpoint : Endpoint<ChannelRequest, ChannelResponse>
{
    public override void Configure()
    {
        Post("channel-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ChannelRequest req, CancellationToken ct)
    {
        var capacity = req.ChannelCapacity > 0 ? req.ChannelCapacity : 10;
        var channel = Channel.CreateBounded<string>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        });

        int written = 0;
        int read = 0;
        var readItems = new List<string>();

        // Write items
        foreach (var item in req.Items)
        {
            if (await channel.Writer.WaitToWriteAsync(ct))
            {
                await channel.Writer.WriteAsync(item, ct);
                written++;
            }
        }
        channel.Writer.Complete();

        // Read items
        await foreach (var item in channel.Reader.ReadAllAsync(ct))
        {
            readItems.Add(item);
            read++;
        }

        await Send.OkAsync(new ChannelResponse
        {
            ItemsWritten = written,
            ItemsRead = read,
            ReadItems = readItems,
            ChannelWorked = written == read && written == req.Items.Count
        });
    }
}
