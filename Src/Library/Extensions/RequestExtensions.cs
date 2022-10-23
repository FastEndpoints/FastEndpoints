using System.Reflection.Metadata;

namespace FastEndpoints.Extensions;

public static class RequestExtensions
{
    public static Task<TResponse> SendAsync<TResponse>(this IRequest<TResponse> requestModel, CancellationToken cancellation = default)
    {
        var requestType = typeof(Request<,>);
        Type[] typeArgs = { requestModel.GetType(), typeof(TResponse) };

        var requestGenericType = requestType.MakeGenericType(typeArgs);
        var request = Activator.CreateInstance(requestGenericType);
        if (request == null)
            throw new Exception($"Couldn't create an instance of the request '{requestGenericType.Name}'!.");

        var sendMethod = request.GetType().GetMethod("SendAsync")!;
        return (Task<TResponse>)sendMethod.Invoke(request, new object[] { requestModel, cancellation })!;
    }
}
