using System.Diagnostics.CodeAnalysis;

namespace FastEndpoints;

/// <summary>
/// stream command middleware configuration
/// </summary>
public class StreamCommandMiddlewareConfig : CommandMiddlewareConfigBase
{
    protected override Type OpenGenericInterface => typeof(IStreamCommandMiddleware<,>);

    /// <summary>
    /// register a closed-generic stream command middleware in the pipeline.
    /// </summary>
    /// <typeparam name="TCommand">the type of the command</typeparam>
    /// <typeparam name="TResult">the type of items in the result stream</typeparam>
    /// <typeparam name="TMiddleware">the type of the middleware</typeparam>
    public void Register<TCommand, TResult, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TMiddleware>()
        where TCommand : IStreamCommand<TResult>
        where TMiddleware : IStreamCommandMiddleware<TCommand, TResult>
        => Middleware.Add((typeof(IStreamCommandMiddleware<TCommand, TResult>), typeof(TMiddleware)));
}
