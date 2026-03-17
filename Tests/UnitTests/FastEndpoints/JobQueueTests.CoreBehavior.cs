using FastEndpoints;
using System.Linq.Expressions;
using Xunit;

namespace JobQueue;

public partial class JobQueueTests
{
    sealed class BasicJob : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class DistributedJob : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
        public DateTime DequeueAfter { get; set; }
    }

    [Fact]
    public void default_dequeue_after_getter_returns_datetime_min_value()
    {
        IJobStorageRecord record = new BasicJob();
        record.DequeueAfter.ShouldBe(default);
    }

    [Fact]
    public void default_dequeue_after_setter_is_no_op()
    {
        IJobStorageRecord record = new BasicJob();
        record.DequeueAfter = DateTime.UtcNow.AddHours(1);
        record.DequeueAfter.ShouldBe(default);
    }

    [Fact]
    public void overridden_dequeue_after_getter_returns_set_value()
    {
        var future = DateTime.UtcNow.AddMinutes(10);
        var record = new DistributedJob { DequeueAfter = future };

        record.DequeueAfter.ShouldBe(future);
    }

    [Fact]
    public void overridden_dequeue_after_setter_persists_value()
    {
        var record = new DistributedJob();
        var future = DateTime.UtcNow.AddMinutes(30);

        record.DequeueAfter.ShouldBe(default);
        record.DequeueAfter = future;
        record.DequeueAfter.ShouldBe(future);
    }

    [Fact]
    public void execution_time_limit_can_be_set_and_retrieved()
    {
        var parameters = new PendingJobSearchParams<BasicJob>
        {
            ExecutionTimeLimit = TimeSpan.FromMinutes(5)
        };

        parameters.ExecutionTimeLimit.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void execution_time_limit_defaults_to_zero()
    {
        var parameters = new PendingJobSearchParams<BasicJob>();

        parameters.ExecutionTimeLimit.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public Task non_distributed_provider_works_without_dequeue_after()
    {
        var now = DateTime.UtcNow;
        var job = new BasicJob
        {
            QueueID = "basic-queue",
            TrackingID = Guid.NewGuid(),
            Command = "basic-cmd",
            ExecuteAfter = now.AddMinutes(-1),
            ExpireOn = now.AddHours(1)
        };

        Expression<Func<BasicJob, bool>> match =
            record => record.QueueID == "basic-queue" &&
                      !record.IsComplete &&
                      record.ExecuteAfter <= now &&
                      record.ExpireOn >= now;

        var result = new[] { job }.Where(match.Compile()).Take(1).ToArray();

        result.Length.ShouldBe(1);
        ((IJobStorageRecord)result[0]).DequeueAfter.ShouldBe(default);

        return Task.CompletedTask;
    }
}
