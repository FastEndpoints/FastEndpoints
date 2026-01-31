using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FastEndpoints;

[UnconditionalSuppressMessage("Trimming", "IL2026"), UnconditionalSuppressMessage("AOT", "IL3050")]
public abstract partial class Endpoint<TRequest, TResponse> where TRequest : notnull
{
    //NOTE:
    //  the following vars are initialized lazily in order to prevent
    //  instantiation during endpoint config phase at startup.
    Http? _httpMethod;
    string? _baseUrl;
    ILogger? _logger;
    IWebHostEnvironment? _env;
    TResponse? _response;
    ResponseSender<TRequest, TResponse>? _sender;
    IConfiguration? _config;

    /// <summary>
    /// the response object that is serialized to the response stream.
    /// </summary>
    [DontInject]
    public TResponse Response
    {
        get => _response is null ? InitResponseDto() : _response;
        set => _response = value;
    }

    /// <summary>
    /// the current user principal
    /// </summary>
    public ClaimsPrincipal User => HttpContext.User;

    /// <summary>
    /// gives access to the app configuration.
    /// </summary>
    public IConfiguration Config => _config ??= ServiceResolver.Instance.Resolve<IConfiguration>();

    /// <summary>
    /// gives access to response sending methods for the endpoint. you can add your own custom response sending methods via extension methods targeting the
    /// <see cref="IResponseSender" /> interface.
    /// </summary>
    protected ResponseSender<TRequest, TResponse> Send => _sender ??= new(this);

    /// <summary>
    /// gives access to the hosting environment
    /// </summary>
    public IWebHostEnvironment Env => _env ??= ServiceResolver.Instance.Resolve<IWebHostEnvironment>();

    /// <summary>
    /// the logger for the current endpoint type
    /// </summary>
    public ILogger Logger => _logger ??= ServiceResolver.Instance.Resolve<ILoggerFactory>().CreateLogger(Definition.EndpointType);

    /// <summary>
    /// the base url of the current request
    /// </summary>
    public string BaseURL => _baseUrl ??= $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host.ToString()}/";

    /// <summary>
    /// the http method of the current request
    /// </summary>
    public Http HttpMethod => _httpMethod ??= Enum.Parse<Http>(HttpContext.Request.Method);

    /// <summary>
    /// the form sent with the request. only populated if content-type is 'application/x-www-form-urlencoded' or 'multipart/form-data'
    /// </summary>
    public IFormCollection Form => HttpContext.Request.Form;

    /// <summary>
    /// the files sent with the request. only populated when content-type is 'multipart/form-data'
    /// </summary>
    public IFormFileCollection Files => Form.Files;

    /// <summary>
    /// get or set whether the response has started. you'd only use this if you're writing to the response stream by yourself.
    /// </summary>
    [DontInject]
    public bool ResponseStarted
    {
        get => HttpContext.ResponseStarted();
        set
        {
            if (value)
                HttpContext.MarkResponseStart();
        }
    }

    static readonly JsonObject _emptyObject = new();
    static readonly JsonArray _emptyArray = [];

    TResponse InitResponseDto()
    {
        if (_isStringResponse) //otherwise strings are detected as IEnumerable of chars
            return default!;

        try
        {
            _response = JsonSerializer.Deserialize<TResponse>(_isCollectionResponse ? _emptyArray : _emptyObject, Cfg.SerOpts.Options)!;
        }
        catch
        {
            //do nothing
        }

        return _response is null
                   ? throw new NotSupportedException(
                         $"Unable to create an instance of the response DTO. Please create it yourself and assign to the [{nameof(Response)}] property!")
                   : _response;
    }
}