namespace FastEndpoints;

/// <summary>
/// interface for a command receiver that can be used to test the receipt of commands in testing.
/// </summary>
/// <typeparam name="TCommand">the type of the command</typeparam>
public interface ICommandReceiver<TCommand> where TCommand : notnull
{
    /// <summary />
    protected internal void AddCommand(TCommand command);

    /// <summary>
    /// waits until at least one matching command is received not exceeding the timeout period.
    /// </summary>
    /// <param name="match">a predicate for matching commands that should be returned by the method</param>
    /// <param name="timeoutSeconds">how long the method will wait until a matching command is received. default value is 2 seconds</param>
    /// <param name="ct">optional cancellation token</param>
    Task<IEnumerable<TCommand>> WaitForMatchAsync(Func<TCommand, bool> match, int timeoutSeconds = 2, CancellationToken ct = default);
}

/// <summary>
/// the default implementation of a command receiver that can be used to test the execution of a command.
/// </summary>
/// <typeparam name="TCommand">the type of the event</typeparam>
public sealed class CommandReceiver<TCommand> : ICommandReceiver<TCommand> where TCommand : notnull
{
    readonly List<TCommand> _received = [];

    void ICommandReceiver<TCommand>.AddCommand(TCommand command)
        => _received.Add(command);

    /// <inheritdoc />
    public async Task<IEnumerable<TCommand>> WaitForMatchAsync(Func<TCommand, bool> match, int timeoutSeconds = 2, CancellationToken ct = default)
    {
        var start = DateTime.Now;

        while (!ct.IsCancellationRequested && DateTime.Now.Subtract(start).TotalSeconds < timeoutSeconds)
        {
            var res = _received.Where(match);

            if (res.Any())
                return res;

            await Task.Delay(100, CancellationToken.None);
        }

        return [];
    }
}