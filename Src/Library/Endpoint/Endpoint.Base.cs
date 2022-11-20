using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

/// <summary>
/// the base class all fast endpoints inherit from
/// </summary>
public abstract class BaseEndpoint : IEndpoint
{
    private List<ValidationFailure> _failures;

    internal abstract Task ExecAsync(CancellationToken ct);

    /// <summary>
    /// gets the endpoint definition which contains all the configuration info for the endpoint
    /// </summary>
    [DontInject]
    public EndpointDefinition Definition { get; internal set; }

    /// <summary>
    /// the http context of the current request
    /// </summary>
    [DontInject]
    public HttpContext HttpContext { get; internal set; }

    /// <summary>
    /// the list of validation failures for the current request dto
    /// </summary>
    public List<ValidationFailure> ValidationFailures => _failures ??= new();

    /// <summary>
    /// use this method to configure how the endpoint should be listening to incoming requests.
    /// <para>HINT: it is only called once during endpoint auto registration during app startup.</para>
    /// </summary>
    [NotImplemented]
    public virtual void Configure() => throw new NotImplementedException();

    public virtual void Verbs(params string[] methods) => throw new NotImplementedException();

    protected internal async ValueTask<TRequest> BindRequest<TRequest>(Type tRequest, CancellationToken ct) where TRequest : notnull, new()
    {
        var binder = (IRequestBinder<TRequest>)
            (Definition.RequestBinder ??= Config.ServiceResolver.Resolve(typeof(IRequestBinder<TRequest>)));

        var binderCtx = new BinderContext(HttpContext, ValidationFailures, Definition.SerializerContext, Definition.DontBindFormData);
        var req = await binder.BindAsync(binderCtx, ct);

        Config.BndOpts.Modifier?.Invoke(req, tRequest, binderCtx, ct);
        return req;
    }

    //this is here just so the derived endpoint class can seal it.
    protected virtual void Group<TEndpointGroup>() where TEndpointGroup : notnull, Group, new() => throw new NotImplementedException();
}