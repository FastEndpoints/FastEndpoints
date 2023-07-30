using IntegrationTests.Shared.Fixtures;
using TestCases.JobQueueTest;
using Xunit;
using Xunit.Abstractions;

namespace FastEndpoints.IntegrationTests.JobQueueTests;

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
            await cmd.QueueJobAsync();
        }

        while (JobTestCommand.CompletedIDs.Count < 10)
        {
            await Task.Delay(10);
        }

        JobTestCommand.CompletedIDs.Count.Should().Be(10);
    }
}
