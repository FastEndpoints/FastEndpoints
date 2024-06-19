using TestCases.JobQueueTest;
using Xunit.Priority;

namespace Messaging;

public class JobQueueTests : TestBase<Sut>
{
    [Fact, Priority(1)]
    public async Task Jobs_Execution()
    {
        for (var i = 0; i < 10; i++)
        {
            var cmd = new JobTestCommand
            {
                Id = i,
                ShouldThrow = i == 0
            };
            await cmd.QueueJobAsync(executeAfter: i == 1 ? DateTime.UtcNow.AddDays(1) : DateTime.UtcNow);
        }

        while (JobTestCommand.CompletedIDs.Count < 9)
            await Task.Delay(10);

        JobTestCommand.CompletedIDs.Count.Should().Be(9);
        var expected = new[] { 0, 2, 3, 4, 5, 6, 7, 8, 9 };
        JobTestCommand.CompletedIDs.Except(expected).Any().Should().BeFalse();
    }

    //[Fact, Priority(2)]
    public async Task Job_Cancellation()
    {
        await Parallel.ForAsync(
            1,
            100,
            async (_, ct) =>
            {
                var job = new JobCancelTestCommand();
                var cancelId = await job.QueueJobAsync(ct: ct);
            });
    }
}