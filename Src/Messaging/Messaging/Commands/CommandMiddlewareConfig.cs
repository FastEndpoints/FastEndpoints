using System.Diagnostics.CodeAnalysis;

namespace FastEndpoints;

/// <summary>
/// command middleware configuration
/// </summary>
public class CommandMiddlewareConfig : CommandMiddlewareConfigBase
{
    protected override Type OpenGenericInterface => typeof(ICommandMiddleware<,>);

    /// <summary>
    /// register a closed-generic command middleware in the pipeline.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of the result</typeparam>
    /// <typeparam name="TMiddleware">the type of the middleware</typeparam>
    public void Register<TCommand, TResult, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TCommand : ICommand<TResult>
        where TMiddleware : ICommandMiddleware<TCommand, TResult>
        => Middleware.Add((typeof(ICommandMiddleware<TCommand, TResult>), typeof(TMiddleware)));
}
