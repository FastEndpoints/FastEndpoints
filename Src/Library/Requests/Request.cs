namespace FastEndpoints;

/// <summary>
/// base class for the request bus
/// </summary>
public abstract class RequestBase
{
    //key: TRequest 
    //val: unique list of concrete request handler instances (subscribers)
    internal static readonly Dictionary<Type, IRequestHandler> handlersDictionary = new();
}

/// <summary>
/// request notification bus which uses an in-process pub/sub messaging system
/// </summary>
/// <typeparam name="TRequest">the type of notification request dto</typeparam>
/// <typeparam name="TResponse">the type of the Response result dto</typeparam>
public class Request<TRequest, TResponse> : RequestBase where TRequest : notnull, IRequest<TResponse>
{
    private readonly IRequestHandler<TRequest, TResponse>? _handler = null;

    /// <summary>
    /// instantiates an request facade for the given request dto type.
    /// </summary>
    public Request()
    {
        if (handlersDictionary.TryGetValue(typeof(TRequest), out var handler))
            _handler = handler as IRequestHandler<TRequest, TResponse>;
    }

    /// <summary>
    /// send the given model/dto to the registered handler of the request
    /// </summary>
    /// <param name="requestModel">the request model/dto to handle</param>
    ///<param name="cancellation">an optional cancellation token</param>
    /// <returns/>a Task of the response result that matches the request type.
    public Task<TResponse> SendAsync(TRequest requestModel, CancellationToken cancellation = default)
    {
        if (_handler == null)
            throw new Exception($"Couldn't find a registered handler for the request of type '{typeof(TRequest).Name}'");

        return _handler.HandleAsync(requestModel, cancellation);
    }
}
