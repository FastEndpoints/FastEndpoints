using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace FastEndpoints;

/// <summary>
/// handler registration options
/// </summary>
public sealed class HandlerRegistry
{
    private readonly IEndpointRouteBuilder routeBuilder;

    internal HandlerRegistry(IEndpointRouteBuilder builder)
    {
        routeBuilder = builder;
    }

    /// <summary>
    /// registers a command handler this server is hosting.
    /// </summary>
    /// <typeparam name="TCommand">the type of the incoming command</typeparam>
    /// <typeparam name="THandler">the type of the handler for the incoming command</typeparam>
    /// <typeparam name="TResult">the type of the result that will be returned from the handler</typeparam>
    public GrpcServiceEndpointConventionBuilder Register<TCommand, THandler, TResult>()
        where TCommand : class, ICommand<TResult>
        where THandler : class, ICommandHandler<TCommand, TResult>
        where TResult : class
            => routeBuilder.MapGrpcService<HandlerExecutor<TCommand, THandler, TResult>>();
}