using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CommandRulesTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public class CommandRulesTestCollection
{
    public const string Name = nameof(CommandRulesTestCollection);
}

[Collection(CommandRulesTestCollection.Name)]
public class CommandRulesTests : IDisposable
{
    public void Dispose()
    {
        var testingProvider = new ServiceCollection().AddHttpContextAccessor().BuildServiceProvider();
        ServiceResolver.Instance = new ServiceResolver(
            provider: testingProvider,
            ctxAccessor: testingProvider.GetRequiredService<IHttpContextAccessor>(),
            isUnitTestMode: true);
    }

    [Fact]
    public async Task RuleEngine_Aggregates_All_Matches_In_Order()
    {
        var services = new ServiceCollection();
        services.AddCommandRules(
            o =>
            {
                o.Register<Input, HighOrderRule>();
                o.Register<Input, LowOrderRule>();
            });

        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<ICommandRuleEngine<Input>>();

        var plan = await engine.EvaluateAsync(new("match"));

        plan.MatchedRuleCount.ShouldBe(2);
        plan.Commands.Select(c => ((TestCommand)c.Command).Name).ShouldBe(["low", "high"]);
    }

    [Fact]
    public async Task RuleEngine_Preserves_Registration_Order_For_Equal_Order_Rules()
    {
        var services = new ServiceCollection();
        services.AddCommandRules(
            o =>
            {
                o.Register<Input, FirstEqualOrderRule>();
                o.Register<Input, SecondEqualOrderRule>();
            });

        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<ICommandRuleEngine<Input>>();

        var plan = await engine.EvaluateAsync(new("equal"));

        plan.MatchedRuleCount.ShouldBe(2);
        plan.Commands.Select(c => ((TestCommand)c.Command).Name).ShouldBe(["first", "second"]);
    }

    [Fact]
    public void Match_Snapshots_Planned_Command_Array()
    {
        var first = new PlannedCommand(new TestCommand("first"));
        var second = new PlannedCommand(new TestCommand("second"));
        var commands = new[] { first };

        var match = CommandRuleMatch.Match(commands);
        commands[0] = second;

        match.Commands[0].ShouldBe(first);
    }

    [Fact]
    public async Task RuleEngine_Stops_After_First_Match_When_Configured()
    {
        var services = new ServiceCollection();
        services.AddCommandRules(
            o =>
            {
                o.MatchMode = CommandRuleMatchMode.First;
                o.Register<Input, HighOrderRule>();
                o.Register<Input, LowOrderRule>();
            });

        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<ICommandRuleEngine<Input>>();

        var plan = await engine.EvaluateAsync(new("match"));

        plan.MatchedRuleCount.ShouldBe(1);
        ((TestCommand)plan.Commands[0].Command).Name.ShouldBe("low");
    }

    [Fact]
    public async Task RuleEngine_Distinguishes_Matched_Empty_Rule_From_No_Match()
    {
        var services = new ServiceCollection();
        services.AddCommandRules(o => o.Register<Input, EmptyRule>());

        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<ICommandRuleEngine<Input>>();

        var plan = await engine.EvaluateAsync(new("empty"));

        plan.HasMatches.ShouldBeTrue();
        plan.MatchedRuleCount.ShouldBe(1);
        plan.Commands.ShouldBeEmpty();
    }

    [Fact]
    public async Task RuleEngine_Throws_Command_Rule_Exception_When_Rule_Returns_Null_Match()
    {
        var engine = new DefaultCommandRuleEngine<Input>([new StaticRule(null!)], new());

        await Should.ThrowAsync<CommandRuleException>(() => engine.EvaluateAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task RuleEngine_Throws_Command_Rule_Exception_When_Rule_Returns_Null_Command_List()
    {
        var engine = new DefaultCommandRuleEngine<Input>([new StaticRule(CreateMatch(null!))], new());

        await Should.ThrowAsync<CommandRuleException>(() => engine.EvaluateAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task RuleEngine_Throws_Command_Rule_Exception_When_Rule_Returns_Null_Planned_Command()
    {
        var engine = new DefaultCommandRuleEngine<Input>([new StaticRule(CreateMatch([null!]))], new());

        await Should.ThrowAsync<CommandRuleException>(() => engine.EvaluateAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task RuleEngine_Throws_Command_Rule_Exception_When_Rule_Returns_Null_Command()
    {
        var engine = new DefaultCommandRuleEngine<Input>([new StaticRule(CommandRuleMatch.Match(new PlannedCommand(null!)))], new());

        await Should.ThrowAsync<CommandRuleException>(() => engine.EvaluateAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task Rule_Build_Snapshots_Command_Enumerable()
    {
        var first = new PlannedCommand(new TestCommand("first"));
        var second = new PlannedCommand(new TestCommand("second"));
        var rule = new MutableBuildRule([first]);

        var match = await rule.EvaluateAsync(new("mutable"));
        rule.Commands[0] = second;

        match.Commands[0].ShouldBe(first);
    }

    [Fact]
    public async Task RuleEngine_Throws_When_No_Rule_Matches_And_Configured()
    {
        var services = new ServiceCollection();
        services.AddCommandRules(
            o =>
            {
                o.UnhandledBehavior = UnhandledRuleBehavior.Throw;
                o.Register<Input, LowOrderRule>();
            });

        await using var provider = services.BuildServiceProvider();
        var engine = provider.GetRequiredService<ICommandRuleEngine<Input>>();

        await Should.ThrowAsync<CommandRuleNotFoundException>(() => engine.EvaluateAsync(new("nope")).AsTask());
    }

    [Fact]
    public async Task Dispatcher_Forced_Mode_Overrides_Planned_Mode()
    {
        ExecutedCommandHandler.Count = 0;

        var planned = new PlannedCommand(new ExecutedCommand()) { Mode = CommandDispatchMode.QueueAsJob };
        var services = new ServiceCollection();
        services.AddMessaging(new[] { new List<Type> { typeof(ExecutedCommandHandler) } });
        services.AddCommandRules(_ => { });
        services.AddSingleton<ICommandRuleEngine<Input>>(new StaticRuleEngine(new CommandRulePlan(1, [planned])));

        await using var provider = services.BuildServiceProvider();
        provider.UseMessaging();

        var dispatcher = provider.GetRequiredService<ICommandDispatcher<Input>>();
        var result = await dispatcher.DispatchAsync(new("match"), CommandDispatchMode.ExecuteNow);

        result.Outcomes.Count.ShouldBe(1);
        result.Outcomes[0].Mode.ShouldBe(CommandDispatchMode.ExecuteNow);
        ExecutedCommandHandler.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteNow_Dispatches_Through_Command_Bus()
    {
        ExecutedCommandHandler.Count = 0;

        var services = new ServiceCollection();
        services.AddMessaging(new[] { new List<Type> { typeof(ExecutedCommandHandler) } });
        services.AddCommandRules(o => o.Register<Input, ExecuteRule>());

        await using var provider = services.BuildServiceProvider();
        provider.UseMessaging();

        var dispatcher = provider.GetRequiredService<ICommandDispatcher<Input>>();
        await dispatcher.DispatchAsync(new("execute"));

        ExecutedCommandHandler.Count.ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteNow_Rejects_Result_Commands()
    {
        var planned = new PlannedCommand(new ResultCommand());
        var dispatcher = new DefaultCommandDispatcher<Input>(new StaticRuleEngine(new CommandRulePlan(1, [planned])), new());

        await Should.ThrowAsync<UnsupportedPlannedCommandException>(() => dispatcher.DispatchAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task ExecuteNow_Rejects_Stream_Commands()
    {
        var planned = new PlannedCommand(new StreamCommand());
        var dispatcher = new DefaultCommandDispatcher<Input>(new StaticRuleEngine(new CommandRulePlan(1, [planned])), new());

        await Should.ThrowAsync<UnsupportedPlannedCommandException>(() => dispatcher.DispatchAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task QueueAsJob_Rejects_Stream_Commands()
    {
        var planned = new PlannedCommand(new StreamCommand());
        var dispatcher = new DefaultCommandDispatcher<Input>(new StaticRuleEngine(new CommandRulePlan(1, [planned])), new());

        await Should.ThrowAsync<UnsupportedPlannedCommandException>(() => dispatcher.DispatchAsync(new("match"), CommandDispatchMode.QueueAsJob).AsTask());
    }

    [Fact]
    public async Task Dispatcher_Throws_Command_Rule_Exception_When_RuleEngine_Returns_Null_Plan()
    {
        var dispatcher = new DefaultCommandDispatcher<Input>(new StaticRuleEngine(null!), new());

        await Should.ThrowAsync<CommandRuleException>(() => dispatcher.DispatchAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task Dispatcher_Throws_Command_Rule_Exception_When_RuleEngine_Returns_Null_Command_List()
    {
        var dispatcher = new DefaultCommandDispatcher<Input>(new StaticRuleEngine(new CommandRulePlan(1, null!)), new());

        await Should.ThrowAsync<CommandRuleException>(() => dispatcher.DispatchAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task Dispatcher_Throws_Command_Rule_Exception_When_RuleEngine_Returns_Negative_Match_Count()
    {
        var dispatcher = new DefaultCommandDispatcher<Input>(new StaticRuleEngine(new CommandRulePlan(-1, [])), new());

        await Should.ThrowAsync<CommandRuleException>(() => dispatcher.DispatchAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task Dispatcher_Throws_Command_Rule_Exception_When_RuleEngine_Returns_Commands_Without_Matches()
    {
        var planned = new PlannedCommand(new TestCommand("matched"));
        var dispatcher = new DefaultCommandDispatcher<Input>(new StaticRuleEngine(new CommandRulePlan(0, [planned])), new());

        await Should.ThrowAsync<CommandRuleException>(() => dispatcher.DispatchAsync(new("match")).AsTask());
    }

    [Fact]
    public async Task QueueAsJob_Validates_Utc_Scheduling_Before_Queueing()
    {
        RegisterTestJobQueue();
        var planned = new PlannedCommand(new TestCommand("queued"))
        {
            Job = new(DateTime.Now.AddMinutes(1), DateTime.UtcNow.AddHours(1))
        };
        var dispatcher = new DefaultCommandDispatcher<Input>(new StaticRuleEngine(new CommandRulePlan(1, [planned])), new());

        await Should.ThrowAsync<ArgumentException>(() => dispatcher.DispatchAsync(new("match"), CommandDispatchMode.QueueAsJob).AsTask());
    }

    [Fact]
    public async Task QueueAsJob_Validates_Expire_On_After_Execute_After()
    {
        RegisterTestJobQueue();
        var executeAfter = DateTime.UtcNow.AddHours(1);
        var planned = new PlannedCommand(new TestCommand("queued"))
        {
            Job = new(executeAfter, executeAfter)
        };
        var dispatcher = new DefaultCommandDispatcher<Input>(new StaticRuleEngine(new CommandRulePlan(1, [planned])), new());

        await Should.ThrowAsync<ArgumentException>(() => dispatcher.DispatchAsync(new("match"), CommandDispatchMode.QueueAsJob).AsTask());
    }

    sealed record Input(string EventType);

    sealed record TestCommand(string Name) : ICommand;

    sealed record ResultCommand : ICommand<string>;

    sealed record StreamCommand : IStreamCommand<string>;

    sealed class HighOrderRule : CommandRule<Input>
    {
        public override int Order => 10;

        public override bool CanHandle(Input input)
            => input.EventType == "match";

        public override IEnumerable<PlannedCommand> Build(Input input)
        {
            yield return new(new TestCommand("high"));
        }
    }

    sealed class LowOrderRule : CommandRule<Input>
    {
        public override int Order => -10;

        public override bool CanHandle(Input input)
            => input.EventType == "match";

        public override IEnumerable<PlannedCommand> Build(Input input)
        {
            yield return new(new TestCommand("low"));
        }
    }

    sealed class EmptyRule : CommandRule<Input>
    {
        public override bool CanHandle(Input input)
            => input.EventType == "empty";

        public override IEnumerable<PlannedCommand> Build(Input input)
            => [];
    }

    sealed class FirstEqualOrderRule : CommandRule<Input>
    {
        public override bool CanHandle(Input input)
            => input.EventType == "equal";

        public override IEnumerable<PlannedCommand> Build(Input input)
        {
            yield return new(new TestCommand("first"));
        }
    }

    sealed class SecondEqualOrderRule : CommandRule<Input>
    {
        public override bool CanHandle(Input input)
            => input.EventType == "equal";

        public override IEnumerable<PlannedCommand> Build(Input input)
        {
            yield return new(new TestCommand("second"));
        }
    }

    sealed class ExecuteRule : CommandRule<Input>
    {
        public override bool CanHandle(Input input)
            => input.EventType == "execute";

        public override IEnumerable<PlannedCommand> Build(Input input)
        {
            yield return new(new ExecutedCommand());
        }
    }

    sealed class ExecutedCommand : ICommand;

    sealed class ExecutedCommandHandler : ICommandHandler<ExecutedCommand>
    {
        public static int Count { get; set; }

        public Task ExecuteAsync(ExecutedCommand command, CancellationToken ct)
        {
            Count++;

            return Task.CompletedTask;
        }
    }

    sealed class StaticRuleEngine(CommandRulePlan plan) : ICommandRuleEngine<Input>
    {
        public ValueTask<CommandRulePlan> EvaluateAsync(Input input, CancellationToken ct = default)
            => ValueTask.FromResult(plan);
    }

    sealed class StaticRule(CommandRuleMatch match) : ICommandRule<Input>
    {
        public ValueTask<CommandRuleMatch> EvaluateAsync(Input input, CancellationToken ct = default)
            => ValueTask.FromResult(match);
    }

    sealed class MutableBuildRule(List<PlannedCommand> commands) : CommandRule<Input>
    {
        public List<PlannedCommand> Commands { get; } = commands;

        public override bool CanHandle(Input input)
            => input.EventType == "mutable";

        public override IEnumerable<PlannedCommand> Build(Input input)
            => Commands;
    }

    static CommandRuleMatch CreateMatch(IReadOnlyList<PlannedCommand> commands)
        => (CommandRuleMatch)typeof(CommandRuleMatch)
                              .GetConstructors(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                              .Single(c => c.GetParameters() is [{ ParameterType: var matchedType }, { ParameterType: var commandsType }] &&
                                           matchedType == typeof(bool) &&
                                           commandsType == typeof(IReadOnlyList<PlannedCommand>))
                              .Invoke([true, commands]);

    static void RegisterTestJobQueue()
        => _ = new JobQueue<TestCommand, FastEndpoints.Void, TestJobRecord, TestJobStorage>(
            new(),
            new TestHostLifetime(default),
            NullLogger<JobQueue<TestCommand, FastEndpoints.Void, TestJobRecord, TestJobStorage>>.Instance);

    sealed class TestJobRecord : IJobStorageRecord
    {
        public string QueueID { get; set; } = "";
        public Guid TrackingID { get; set; }
        public object Command { get; set; } = null!;
        public DateTime ExecuteAfter { get; set; }
        public DateTime ExpireOn { get; set; }
        public bool IsComplete { get; set; }
    }

    sealed class TestJobStorage : IJobStorageProvider<TestJobRecord>
    {
        public bool DistributedJobProcessingEnabled => false;

        public Task StoreJobAsync(TestJobRecord r, CancellationToken ct)
            => Task.CompletedTask;

        public Task<ICollection<TestJobRecord>> GetNextBatchAsync(PendingJobSearchParams<TestJobRecord> parameters)
            => Task.FromResult<ICollection<TestJobRecord>>([]);

        public Task MarkJobAsCompleteAsync(TestJobRecord r, CancellationToken ct)
            => Task.CompletedTask;

        public Task CancelJobAsync(Guid trackingId, CancellationToken ct)
            => Task.CompletedTask;

        public Task OnHandlerExecutionFailureAsync(TestJobRecord r, Exception exception, CancellationToken ct)
            => Task.CompletedTask;

        public Task PurgeStaleJobsAsync(StaleJobSearchParams<TestJobRecord> parameters)
            => Task.CompletedTask;
    }

    sealed class TestHostLifetime(CancellationToken applicationStopping) : IHostApplicationLifetime
    {
        public CancellationToken ApplicationStarted => default;
        public CancellationToken ApplicationStopping => applicationStopping;
        public CancellationToken ApplicationStopped => default;

        public void StopApplication()
        {
        }
    }

}