using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace FastEndpoints;

public abstract class BaseEndpoint : IEndpoint
{
    internal HttpContext _httpContext; //this is set at the start of ExecAsync() method of each endpoint instance
    private List<ValidationFailure> _failures;
    private IConfiguration? _config;

    internal abstract Task ExecAsync(HttpContext ctx, EndpointDefinition endpoint, CancellationToken ct);

    public virtual void Verbs(params Http[] methods) => throw new NotImplementedException();

    /// <summary>
    /// gets the endpoint definition which contains all the configuration info for the endpoint
    /// </summary>
    public EndpointDefinition Definition { get; internal set; }

    /// <summary>
    /// gives access to the configuration. if you need to access this property from within the endpoint Configure() method, make sure to pass in the config to <c>.AddFastEndpoints(config: builder.Configuration)</c>
    /// </summary>
    public IConfiguration? Config {
        get => _config ??= _httpContext?.RequestServices.GetRequiredService<IConfiguration>();
        internal set => _config = value;
    }

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

    /// <summary>
    /// gets a stream of nullable FileMultipartSections from the incoming multipart/form-data without buffering the whole file to memory/disk as done with IFormFile
    /// </summary>
    /// <param name="cancellation">optional cancellation token</param>
    public async IAsyncEnumerable<FileMultipartSection?> FormFileSectionsAsync([EnumeratorCancellation] CancellationToken cancellation = default)
    {
        var reader = new MultipartReader(HttpContext.Request.GetMultipartBoundary(), HttpContext.Request.Body);

        MultipartSection? section;

        while ((section = await reader.ReadNextSectionAsync(cancellation)) is not null)
        {
            if (section.GetContentDispositionHeader()?.IsFileDisposition() is true)
                yield return section.AsFileSection();
        }
    }
}