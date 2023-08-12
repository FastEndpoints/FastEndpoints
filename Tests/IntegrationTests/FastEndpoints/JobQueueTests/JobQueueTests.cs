using FastEndpoints;
using Shared.Fixtures;
using TestCases.JobQueueTest;
using Xunit;
using Xunit.Abstractions;

namespace JobQueues;

public class JobQueueTests : EndToEndTestBase
{
    public JobQueueTests(EndToEndTestFixture endToEndTestFixture, ITestOutputHelper outputHelper) : base(endToEndTestFixture, outputHelper)
    {
    }

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
