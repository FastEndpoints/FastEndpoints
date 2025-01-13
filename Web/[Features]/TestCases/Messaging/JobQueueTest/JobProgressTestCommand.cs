namespace TestCases.JobQueueTest;

public class JobProgressTestCommand : ITrackableJob<JobResult<string>>
{
    public static int CurrentStep { get; set; }

    public Guid TrackingID { get; set; }
    public string Name { get; set; }
}

public class JobProgressTestCmdHandler(IJobTracker<JobProgressTestCommand> tracker) : ICommandHandler<JobProgressTestCommand, JobResult<string>>
{
    public async Task<JobResult<string>> ExecuteAsync(JobProgressTestCommand cmd, CancellationToken ct)
    {
        var jobRes = new JobResult<string>(3);

        var step = 1;

        while (true)
        {
            if (JobProgressTestCommand.CurrentStep == 3)
                break;

            if (JobProgressTestCommand.CurrentStep == step - 1)
            {
                jobRes.CurrentStep = step;
                await tracker.StoreJobResultAsync(cmd.TrackingID, jobRes, ct);
                step++;
            }
            else
                await Task.Delay(100);
        }

        jobRes.Result = cmd.Name;

        return jobRes;
    }
}