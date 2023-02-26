namespace FastEndpoints;

/// <summary>
/// create custom request binders by implementing this interface. by registering a custom modelbinder for an endpoint will completely disable the built-in model binding and completely depend on your implementation of the custom binder to return a correctly populated request dto for the endpoint.
/// </summary>
/// <typeparam name="TRequest">the type of the request dto</typeparam>
public interface IRequestBinder<TRequest> where TRequest : notnull
{
    /// <summary>
    /// this method will be called by the library for binding the incoming request data and return a populated request dto object.
    /// access the incoming request data via the <c>RequestBinderContext</c> and populate a new request dto instance and return it from this method.
    /// </summary>
    /// <param name="ctx">request binder context encapsulating the incoming http request context, a list of validation failures for the endpoint, and an optional json serializer context.</param>
    /// <param name="ct">cancellation token</param>
    public ValueTask<TRequest> BindAsync(BinderContext ctx, CancellationToken ct);
}
