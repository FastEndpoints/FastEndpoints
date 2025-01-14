using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace Contracts;

sealed class CreateOrderRequest
{
    public decimal Total { get; set; }
}

sealed class PublishOrderCreatedEventCommand : ICommand
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }
    public decimal Total { get; set; }
}

public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; }
    public decimal Total { get; set; }
}

public class JobRecord : IJobStorageRecord
{
    public Guid Id { get; set; }
    public string QueueID { get; set; } = default!;
    public Guid TrackingID { get; set; }
    public DateTime ExecuteAfter { get; set; }
    public DateTime ExpireOn { get; set; }
    public bool IsComplete { get; set; }

    [NotMapped]
    public object Command { get; set; } = default!;

    public string CommandJson { get; set; } = default!;

    TCommand IJobStorageRecord.GetCommand<TCommand>()
        => JsonSerializer.Deserialize<TCommand>(CommandJson)!;

    void IJobStorageRecord.SetCommand<TCommand>(TCommand command)
        => CommandJson = JsonSerializer.Serialize(command);
}

public class OrderDbContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<JobRecord> JobRecords { get; set; }

    public OrderDbContext()
    {
    }

    public OrderDbContext(DbContextOptions<OrderDbContext> options) : base(options)
    {
    }
}

public class JobStorageProvider : IJobStorageProvider<JobRecord>
{
    readonly IDbContextFactory<OrderDbContext> _dbContextFactory;

    public JobStorageProvider(IDbContextFactory<OrderDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public async Task StoreJobAsync(JobRecord job, CancellationToken ct)
    {
        using var db = _dbContextFactory.CreateDbContext();
        db.JobRecords.Add(job);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IEnumerable<JobRecord>> GetNextBatchAsync(PendingJobSearchParams<JobRecord> p)
    {
        using var db = _dbContextFactory.CreateDbContext();

        return await db.JobRecords
                       .Where(p.Match)
                       .Take(p.Limit)
                       .ToListAsync(p.CancellationToken);
    }

    public async Task MarkJobAsCompleteAsync(JobRecord job, CancellationToken c)
    {
        using var db = _dbContextFactory.CreateDbContext();
        await db.JobRecords.Where(x => x.Id == job.Id).ExecuteUpdateAsync(up => up.SetProperty(p => p.IsComplete, true), c);
    }

    public async Task CancelJobAsync(Guid trackingId, CancellationToken c)
    {
        using var db = _dbContextFactory.CreateDbContext();
        await db.JobRecords.Where(x => x.TrackingID == trackingId).ExecuteUpdateAsync(up => up.SetProperty(p => p.IsComplete, true), c);
    }

    public async Task OnHandlerExecutionFailureAsync(JobRecord job, Exception e, CancellationToken c)
    {
        using var db = _dbContextFactory.CreateDbContext();
        await db.JobRecords.Where(x => x.Id == job.Id).ExecuteUpdateAsync(up => up.SetProperty(p => p.ExecuteAfter, DateTime.UtcNow.AddMinutes(1)), c);
    }

    public async Task PurgeStaleJobsAsync(StaleJobSearchParams<JobRecord> p)
    {
        using var db = _dbContextFactory.CreateDbContext();
        var staleJobs = db.JobRecords.Where(p.Match);
        db.RemoveRange(staleJobs);
        await db.SaveChangesAsync(p.CancellationToken);
    }
}

class PublishOrderCreatedEventCommandHandler : ICommandHandler<PublishOrderCreatedEventCommand>
{
    readonly ILogger<PublishOrderCreatedEventCommandHandler> _logger;

    public PublishOrderCreatedEventCommandHandler(ILogger<PublishOrderCreatedEventCommandHandler> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(PublishOrderCreatedEventCommand command, CancellationToken ct)
    {
        _logger.LogError("Queueing OrderCreated event {OrderNumber} to RabbitMQ, Azure ServiceBus, Redis Streams, Azure EventGrid...", command.OrderNumber);
        return Task.CompletedTask;
    }
}

sealed class OrderEndpoint : Endpoint<CreateOrderRequest>
{
    readonly IDbContextFactory<OrderDbContext> _dbContextFactory;

    public OrderEndpoint(IDbContextFactory<OrderDbContext> dbContextFactory)
    {
        _dbContextFactory = dbContextFactory;
    }

    public override void Configure()
    {
        Get("orders");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CreateOrderRequest req, CancellationToken ct)
    {
        var order = new Order()
        {
            OrderNumber = "1234",
            Total = req.Total
        };

        using (var dbContext = _dbContextFactory.CreateDbContext())
        {
            dbContext.Database.OpenConnection();
            await dbContext.Database.EnsureCreatedAsync(ct);
            using var transaction = dbContext.Database.BeginTransaction();
            try
            {
                dbContext.Orders.Add(order);
                await dbContext.SaveChangesAsync(ct);

                var @eventCommand = new PublishOrderCreatedEventCommand()
                {
                    Id = order.Id,
                    OrderNumber = order.OrderNumber,
                    Total = order.Total
                };
                var job = @eventCommand.CreateJob<JobRecord>();
                dbContext.JobRecords.Add(job);
                await dbContext.SaveChangesAsync(ct);

                transaction.Commit();
                @eventCommand.TriggerJobExecution();
            }
            catch (Exception)
            {
                transaction.Rollback();
                await SendErrorsAsync(StatusCodes.Status500InternalServerError, ct);
            }
        }

        await SendAsync(order.Id);
    }
}