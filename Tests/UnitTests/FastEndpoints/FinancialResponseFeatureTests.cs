using FastEndpoints;
using Microsoft.AspNetCore.Http.Features;
using Shouldly;
using Xunit;

namespace Unit.FastEndpoints;

public class FinancialResponseFeatureTests
{
    [Fact]
    public async Task No_Callbacks_Can_Be_Run_Or_Transferred()
    {
        var response = new FinancialResponseFeature(new HttpResponseFeature());
        await using var capture = new FinancialResponseCapture(response, 1024);

        await response.RunStartingAsync();
        response.TransferStartingTo(capture);
        await capture.SnapshotAsync();
        await response.RunStartingAsync();

        capture.HasStarted.ShouldBeTrue();
    }

    [Fact]
    public async Task Callbacks_Registered_During_Starting_Run_In_Order_Only_Once()
    {
        var response = new FinancialResponseFeature(new HttpResponseFeature());
        var callbacks = new List<int>();
        response.OnStarting(_ => { callbacks.Add(1); return Task.CompletedTask; }, response);
        response.OnStarting(async _ =>
        {
            callbacks.Add(2);
            await Task.Yield();
            response.OnStarting(_ => { callbacks.Add(3); return Task.CompletedTask; }, response);
        }, response);

        await response.RunStartingAsync();
        await response.RunStartingAsync();

        callbacks.ShouldBe([2, 3, 1]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Failed_Callback_Clears_Pending_Callbacks(bool asynchronous)
    {
        var response = new FinancialResponseFeature(new HttpResponseFeature());
        var callbacks = new List<int>();
        var failure = new InvalidOperationException("Callback failed.");
        response.OnStarting(_ => { callbacks.Add(1); return Task.CompletedTask; }, response);
        response.OnStarting(_ =>
        {
            callbacks.Add(2);

            return asynchronous ? FailAsync() : throw failure;
        }, response);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() => response.RunStartingAsync());
        thrown.ShouldBeSameAs(failure);
        await response.RunStartingAsync();

        await using var capture = new FinancialResponseCapture(response, 1024);
        response.TransferStartingTo(capture);
        await capture.SnapshotAsync();
        callbacks.ShouldBe([2]);

        async Task FailAsync()
        {
            await Task.Yield();
            throw failure;
        }
    }

    [Fact]
    public async Task Transferred_Callbacks_Run_In_Order_Only_Once()
    {
        var response = new FinancialResponseFeature(new HttpResponseFeature());
        var callbacks = new List<int>();
        response.OnStarting(_ => { callbacks.Add(1); return Task.CompletedTask; }, response);
        response.OnStarting(_ => { callbacks.Add(2); return Task.CompletedTask; }, response);
        await using var capture = new FinancialResponseCapture(response, 1024);

        response.TransferStartingTo(capture);
        response.TransferStartingTo(capture);
        await response.RunStartingAsync();
        await capture.SnapshotAsync();
        await response.RunStartingAsync();

        callbacks.ShouldBe([2, 1]);
    }
}
