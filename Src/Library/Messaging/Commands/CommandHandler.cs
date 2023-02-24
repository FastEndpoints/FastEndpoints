using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;

namespace FastEndpoints;

/// <summary>
/// the base class from which all <see cref="CommandHandler{TCommand}"/> classes inherit from
/// </summary>
/// <typeparam name="TCommand">the type of the command</typeparam>
public abstract class CommandHandlerBase<TCommand> : IValidationErrors<TCommand>
{
    ///<inheritdoc/>
    public List<ValidationFailure> ValidationFailures { get; } =
        (List<ValidationFailure>?)
            Config.ServiceResolver?.TryResolve<IHttpContextAccessor>()?.HttpContext?.Items[CtxKey.ValidationFailures] ??
                new();

    ///<inheritdoc/>
    public bool ValidationFailed => ValidationFailures.ValidationFailed();

    ///<inheritdoc/>
    public void AddError(string message, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(message, errorCode, severity);

    ///<inheritdoc/>
    public void AddError(Expression<Func<TCommand, object>> property, string errorMessage, string? errorCode = null, Severity severity = Severity.Error)
        => ValidationFailures.AddError(property, errorMessage, errorCode, severity);

    ///<inheritdoc/>
    [DoesNotReturn]
    public void ThrowError(string message)
        => ValidationFailures.ThrowError(message);

    ///<inheritdoc/>
    [DoesNotReturn]
    public void ThrowError(Expression<Func<TCommand, object>> property, string errorMessage)
        => ValidationFailures.ThrowError(property, errorMessage);

    ///<inheritdoc/>
    public void ThrowIfAnyErrors() =>
        ValidationFailures.ThrowIfAnyErrors();
}

/// <summary>
/// inherit this base class if you'd like to manipulate validation state of the calling endpoint from within the command handler.
/// </summary>
/// <typeparam name="TCommand">the type of the command that will be handled by this command handler</typeparam>
public abstract class CommandHandler<TCommand> : CommandHandlerBase<TCommand>, ICommandHandler<TCommand> where TCommand : ICommand
{
    ///<inheritdoc/>
    public abstract Task ExecuteAsync(TCommand command, CancellationToken ct = default);
}

/// <summary>
/// inherit this base class if you'd like to manipulate validation state of the calling endpoint from within the command handler.
/// </summary>
/// <typeparam name="TCommand">the type of the command that will be handled by this command handler</typeparam>
/// <typeparam name="TResult">the type of the result that will be returned by this command handler</typeparam>
public abstract class CommandHandler<TCommand, TResult> : CommandHandlerBase<TCommand>, ICommandHandler<TCommand, TResult> where TCommand : ICommand<TResult>
{
    ///<inheritdoc/>
    public abstract Task<TResult> ExecuteAsync(TCommand command, CancellationToken ct = default);
}