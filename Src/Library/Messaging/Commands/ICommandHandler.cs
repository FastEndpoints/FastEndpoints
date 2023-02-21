using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// marker interface for all command handlers
/// </summary>
public interface ICommandHandler { }

/// <summary>
/// interface to be implemented by a command handler for a given command type that does not return a result
/// </summary>
/// <typeparam name="TCommand">the type of the command</typeparam>
public interface ICommandHandler<in TCommand> : ICommandHandler where TCommand : ICommand
{
    /// <summary>
    /// accepts a command and does not return a result.
    /// </summary>
    /// <param name="command">the input command object</param>
    /// <param name="ct">optional cancellation token</param>
    Task ExecuteAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>
/// interface to be implemented by a command handler for a given command type that returns a result
/// </summary>
/// <typeparam name="TCommand">the type of the input command</typeparam>
/// <typeparam name="TResult">the type of the result returned</typeparam>
public interface ICommandHandler<in TCommand, TResult> : ICommandHandler where TCommand : ICommand<TResult>
{
    /// <summary>
    /// accepts a command and returns a result.
    /// </summary>
    /// <param name="command">the input command object</param>
    /// <param name="ct">optional cancellation token</param>
    Task<TResult> ExecuteAsync(TCommand command, CancellationToken ct = default);
}

public abstract class CommandHandler<TCommand> : ICommandHandler<TCommand>, IValidationErrors<TCommand> where TCommand : ICommand
{
    ///<inheritdoc/>
    public List<ValidationFailure> ValidationFailures { get; } =
        (List<ValidationFailure>?)Config.ServiceResolver.Resolve<IHttpContextAccessor>().HttpContext?.Items[CtxKey.ValidationFailures] ??
            throw new NotSupportedException($"Executing command handlers derived from [{nameof(CommandHandler<TCommand>)}] outside of endpoints is not supported!");

    ///<inheritdoc/>
    public bool ValidationFailed => ValidationFailures.Count > 0;

    ///<inheritdoc/>
    public abstract Task ExecuteAsync(TCommand command, CancellationToken ct = default);
}

//public class test : CommandHandler<ICommand>
//{
//    public override Task ExecuteAsync(ICommand command, CancellationToken ct = default) { 
//    add
//    }
//}
