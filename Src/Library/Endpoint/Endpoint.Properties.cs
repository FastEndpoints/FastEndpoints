using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using static FastEndpoints.Config;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull
{
    private Http? _httpMethod;
    private string _baseURL;
    private ILogger _logger;
    private IWebHostEnvironment _env;
    private TResponse _response;
    private IConfiguration? _config;

    /// <summary>
    /// the current user principal
    /// </summary>
    public ClaimsPrincipal User => HttpContext.User;

    /// <summary>
    /// the response that is sent to the client.
    /// </summary>
    [DontInject]
    public TResponse Response {
        get => _response is null ? InitResponseDTO() : _response;
        set => _response = value;
    }

    /// <summary>
    /// gives access to the configuration. if you need to access this property from within the endpoint Configure() method, make sure to pass in the config to <c>.AddFastEndpoints(config: builder.Configuration)</c>
    /// </summary>
    [DontInject]
    public IConfiguration Config {
        get => _config ??= FastEndpoints.Config.ServiceResolver.Resolve<IConfiguration>();
        internal set => _config = value;
    }

    /// <summary>
    /// gives access to the hosting environment
    /// </summary>
    public IWebHostEnvironment Env => _env ??= FastEndpoints.Config.ServiceResolver.Resolve<IWebHostEnvironment>();

    /// <summary>
    /// the logger for the current endpoint type
    /// </summary>
    public ILogger Logger => _logger ??= FastEndpoints.Config.ServiceResolver.Resolve<ILoggerFactory>().CreateLogger(Definition.EndpointType);

    /// <summary>
    /// the base url of the current request
    /// </summary>
    public string BaseURL => _baseURL ??= $"{HttpContext.Request?.Scheme}://{HttpContext.Request?.Host.ToString()}/";

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
    public bool ResponseStarted {
        get => HttpContext.ResponseStarted();
        set => HttpContext.MarkResponseStart();
    }

    private static readonly JsonObject emptyObject = new();
    private static readonly JsonArray emptyArray = new();
    private TResponse InitResponseDTO()
    {
        if (isStringResponse) //otherwise strings are detected as IEnumerable of chars
            return default!;

        _response = JsonSerializer.Deserialize<TResponse>(
            isCollectionResponse ? emptyArray : emptyObject,
            SerOpts.Options)!;

        return _response is null
            ? throw new NotSupportedException($"Unable to create an instance of the response DTO. Please create it yourself and assign to the [{nameof(Response)}] property!")
            : _response;
    }
}
