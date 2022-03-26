using FluentValidation.Results;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

public abstract class BaseEndpoint : IEndpoint
{
    protected internal HttpContext _httpContext; //this is set at the start of ExecAsync() method of each endpoint instance

    public EndpointDefinition Configuration { get; internal set; }

    private List<ValidationFailure> _failures;

    internal abstract Task ExecAsync(HttpContext ctx, EndpointDefinition endpoint, CancellationToken ct);

    public virtual void Verbs(params Http[] methods) => throw new NotImplementedException();

    /// <summary>
    /// the http context of the current request
    /// </summary>
    public HttpContext HttpContext => _httpContext;

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
}
