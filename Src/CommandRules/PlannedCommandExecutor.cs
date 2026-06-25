namespace FastEndpoints;

static class PlannedCommandExecutor
{
    public static ValueTask<CommandDispatchOutcome> DispatchAsync(PlannedCommand plannedCommand, CommandDispatchMode mode, CancellationToken ct)
        => mode switch
        {
            CommandDispatchMode.ExecuteNow => ExecuteNowAsync(plannedCommand.Command, ct),
            CommandDispatchMode.QueueAsJob => QueueAsJobAsync(plannedCommand, ct),
            _ => throw new CommandRuleException($"Unsupported command dispatch mode [{mode}].")
        };

    static async ValueTask<CommandDispatchOutcome> ExecuteNowAsync(ICommandBase command, CancellationToken ct)
    {
        var commandType = command.GetType();
        var commandInfo = CommandInterfaceInfo.For(commandType);

        if (commandInfo.IsVoidCommand)
        {
            await ((ICommand)command).ExecuteAsync(ct);

            return CommandDispatchOutcome.Successful(command, CommandDispatchMode.ExecuteNow);
        }

        if (commandInfo.ImplementsStreamCommand)
            throw new UnsupportedPlannedCommandException(commandType, "Stream commands are not supported by ExecuteNow dispatch.");

        if (commandInfo.IsResultCommand)
            throw new UnsupportedPlannedCommandException(commandType, "ICommand<TResult> execution is not supported by command rules yet.");

        throw new UnsupportedPlannedCommandException(commandType, "The command must implement ICommand.");
    }

    static async ValueTask<CommandDispatchOutcome> QueueAsJobAsync(PlannedCommand plannedCommand, CancellationToken ct)
    {
        ValidateQueueAsJobCommand(plannedCommand);

        var job = plannedCommand.Job;
        var trackingId = await plannedCommand.Command.QueueJobAsync(job?.ExecuteAfter, job?.ExpireOn, ct);

        return CommandDispatchOutcome.Successful(plannedCommand.Command, CommandDispatchMode.QueueAsJob, trackingId);
    }

    static void ValidateQueueAsJobCommand(PlannedCommand plannedCommand)
    {
        var command = plannedCommand.Command;
        var commandType = command.GetType();
        var commandInfo = CommandInterfaceInfo.For(commandType);

        if (commandType.ContainsGenericParameters)
            throw new UnsupportedPlannedCommandException(commandType, "Open generic commands are not supported by QueueAsJob dispatch.");

        if (commandInfo.ImplementsStreamCommand)
            throw new UnsupportedPlannedCommandException(commandType, "Stream commands are not supported by QueueAsJob dispatch.");

        if (!commandInfo.ImplementsGenericCommand)
            throw new UnsupportedPlannedCommandException(commandType, "The command must implement ICommand<TResult> to be queued as a job.");
    }
}