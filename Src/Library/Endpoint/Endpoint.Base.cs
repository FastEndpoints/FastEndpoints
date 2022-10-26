using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace FastEndpoints;

/// <summary>
/// the base class all fast endpoints inherit from
/// </summary>
public abstract class BaseEndpoint : IEndpoint
{
    private List<ValidationFailure> _failures;
    private IConfiguration? _config;

    internal abstract Task ExecAsync(CancellationToken ct);

    /// <summary>
    /// gets the endpoint definition which contains all the configuration info for the endpoint
    /// </summary>
    [DontInject]
    public EndpointDefinition Definition { get; internal set; }

    /// <summary>
    /// gives access to the configuration. if you need to access this property from within the endpoint Configure() method, make sure to pass in the config to <c>.AddFastEndpoints(config: builder.Configuration)</c>
    /// </summary>
    public IConfiguration Config {
        get => _config ??= FastEndpoints.Config.ServiceResolver.Resolve<IConfiguration>();
        internal set => _config = value;
    }

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

    protected virtual void Group<TEndpointGroup>() where TEndpointGroup : notnull, Group, new() => throw new NotImplementedException();

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