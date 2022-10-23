namespace FastEndpoints;

internal interface IRequestHandler
{
}

internal interface IRequestHandler<in TRequest, TResponse>:IRequestHandler
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> HandleAsync(TRequest request, CancellationToken ct);
}

internal interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, int>
    where TRequest : IRequest<int>
{
}
public interface IRequest<out TResponse>
{
}

public interface IRequest: IRequest<int> { }