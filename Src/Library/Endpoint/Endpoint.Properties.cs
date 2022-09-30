using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using static FastEndpoints.Config;

namespace FastEndpoints;

public abstract partial class Endpoint<TRequest, TResponse> : BaseEndpoint where TRequest : notnull, new()
{
    private Http? _httpMethod;
    private string _baseURL;
    private ILogger _logger;
    private IWebHostEnvironment _env;
    private TResponse _response;

    /// <summary>
    /// indicates if there are any validation failures for the current request
    /// </summary>
    public bool ValidationFailed => ValidationFailures.Count > 0;

    /// <summary>
    /// the current user principal
    /// </summary>
    public ClaimsPrincipal User => HttpContext.User;

    /// <summary>
    /// the response that is sent to the client.
    /// </summary>
    public TResponse Response {
        get => _response is null ? InitResponseDTO() : _response;
        set => _response = value;
    }

    /// <summary>
    /// gives access to the hosting environment
    /// </summary>
    public IWebHostEnvironment Env => _env ??= HttpContext.RequestServices.GetRequiredService<IWebHostEnvironment>();

    /// <summary>
    /// the logger for the current endpoint type
    /// </summary>
    public ILogger Logger => _logger ??= HttpContext.RequestServices.GetRequiredService<ILogger<Endpoint<TRequest, TResponse>>>();

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
    public bool ResponseStarted {
        get => HttpContext.ResponseStarted();
        set => HttpContext.MarkResponseStart();
    }

    private static readonly JsonObject emptyObject = new();
    private static readonly JsonArray emptyArray = new();
    private TResponse InitResponseDTO()
    {
        _response = JsonSerializer.Deserialize<TResponse>(
            isCollectionResponse ? emptyArray : emptyObject,
            SerOpts.Options)!;

        return _response is null
            ? throw new NotSupportedException($"Unable to create an instance of the response DTO. Please create it yourself and assign to the [{nameof(Response)}] property!")
            : _response;
    }
}
