namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new() where TResponse : notnull, new()
{
    /// <summary>
    /// override this method if you'd like to do something to the request dto before it gets validated.
    /// </summary>
    /// <param name="req">the request dto</param>
    protected virtual void OnBeforeValidate(TRequest req) { }

    /// <summary>
    /// override this method if you'd like to do something to the request dto before it gets validated.
    /// </summary>
    /// <param name="req">the request dto</param>
    protected virtual Task OnBeforeValidateAsync(TRequest req) => Task.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do something to the request dto after it gets validated.
    /// </summary>
    /// <param name="req">the request dto</param>
    protected virtual void OnAfterValidate(TRequest req) { }

    /// <summary>
    /// override this method if you'd like to do something to the request dto after it gets validated.
    /// </summary>
    /// <param name="req">the request dto</param>
    protected virtual Task OnAfterValidateAsync(TRequest req) => Task.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do something to the request dto before the handler is executed.
    /// </summary>
    /// <param name="req">the request dto</param>
    protected virtual void OnBeforeHandle(TRequest req) { }

    /// <summary>
    /// override this method if you'd like to do something to the request dto before the handler is executed.
    /// </summary>
    /// <param name="req">the request dto</param>
    protected virtual Task OnBeforeHandleAsync(TRequest req) => Task.CompletedTask;

    /// <summary>
    /// override this method if you'd like to do something after the handler is executed.
    /// </summary>
    /// <param name="req">the request dto</param>
    /// <param name="res">the response dto that was sent to the client</param>
    protected virtual void OnAfterHandle(TRequest req, TResponse res) { }

    /// <summary>
    /// override this method if you'd like to do something after the handler is executed.
    /// </summary>
    /// <param name="req">the request dto</param>
    /// <param name="res">the response dto that was sent to the client</param>
    protected virtual Task OnAfterHandleAsync(TRequest req, TResponse res) => Task.CompletedTask;
}
