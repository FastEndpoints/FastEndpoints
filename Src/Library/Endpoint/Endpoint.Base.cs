using FastEndpoints.Validation;
using Microsoft.AspNetCore.Http;
using System.Text;

namespace FastEndpoints;

public abstract class BaseEndpoint : IEndpoint
{
    protected internal HttpContext _httpContext; //this is set at the start of ExecAsync() method of each endpoint instance

    //key: the type of the endpoint
    internal static Dictionary<Type, string> TestURLCache { get; } = new();

    public EndpointDefinition Configuration { get; internal set; }

    internal abstract Task ExecAsync(HttpContext ctx, EndpointDefinition endpoint, CancellationToken ct);

    internal void AddTestURLToCache(Type epType)
    {
        if (Configuration.Routes is null)
            throw new InvalidOperationException($"AddTestURLToCache()[{nameof(Configuration.Routes)}]");

        TestURLCache[epType] = new StringBuilder().BuildRoute(Configuration.Version.Current, Configuration.Routes[0]);
    }

    /// <summary>
    /// the http context of the current request
    /// </summary>
    public HttpContext HttpContext => _httpContext;

    /// <summary>
    /// the list of validation failures for the current request dto
    /// </summary>
    public List<ValidationFailure> ValidationFailures { get; } = new();

    /// <summary>
    /// use this method to configure how the endpoint should be listening to incoming requests.
    /// <para>HINT: it is only called once during endpoint auto registration during app startup.</para>
    /// </summary>
    public abstract void Configure();
}
