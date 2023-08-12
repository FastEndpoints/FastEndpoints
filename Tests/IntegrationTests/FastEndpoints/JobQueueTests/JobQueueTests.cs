using FastEndpoints;
using Shared;
using TestCases.JobQueueTest;
using Xunit;

namespace JobQueues;

public class JobQueueTests : TestBase
{
    public JobQueueTests(WebFixture fixture) : base(fixture) { }

    [Fact]
    public async Task JobsExecuteSuccessfully()
    {
        for (var i = 0; i < 10; i++)
        {
            var cmd = new JobTestCommand
            {
                Id = i,
                ShouldThrow = i == 0
            };
            await cmd.QueueJobAsync(
                executeAfter: i == 1 ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow);
        }

        while (JobTestCommand.CompletedIDs.Count < 9)
        {
            await Task.Delay(10);
        }

        JobTestCommand.CompletedIDs.Count.Should().Be(9);
        var expected = new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 };
        JobTestCommand.CompletedIDs.Except(expected).Any().Should().BeFalse();
    }
}
