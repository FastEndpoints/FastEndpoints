using FastEndpoints.Validation;
using Microsoft.AspNetCore.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;

#pragma warning disable CA1822, IDE1006

[assembly: InternalsVisibleTo("FastEndpoints.Swagger")]
namespace FastEndpoints;
/// <summary>
/// global configuration settings for FastEndpoints
/// </summary>
public class Config
{
    internal static JsonSerializerOptions SerializerOpts { get; set; } = new(); //should only be set from UseFastEndpoints() during startup
    internal static VersioningOptions? VersioningOpts { get; private set; }
    internal static RoutingOptions? RoutingOpts { get; private set; }
    internal static Func<EndpointDefinition, bool>? EpRegFilterFunc { get; private set; }
    internal static Func<List<ValidationFailure>, object> ErrRespBldrFunc { get; private set; }
        = failures => new ErrorResponse(failures);
    internal static Func<HttpRequest, Type, CancellationToken, ValueTask<object?>> ReqDeserializerFunc { get; private set; }
        = (req, tReqDto, cancellation) => req.ReadFromJsonAsync(tReqDto, SerializerOpts, cancellation);
    internal static Func<HttpResponse, object, string, CancellationToken, Task> RespSerializerFunc { get; private set; }
        = (rsp, dto, contentType, cancellation)
            => rsp.WriteAsJsonAsync(dto, SerializerOpts, contentType, cancellation);

    /// <summary>
    /// settings for configuring the json serializer
    /// </summary>
    public Action<JsonSerializerOptions> SerializerOptions { set => value(SerializerOpts); }

    /// <summary>
    /// options for enabling endpoint versioning support
    /// </summary>
    public Action<VersioningOptions> VersioningOptions
    {
        set
        {
            VersioningOpts = new();
            value(VersioningOpts);
        }
    }

    /// <summary>
    /// routing options for all endpoints
    /// </summary>
    public Action<RoutingOptions> RoutingOptions
    {
        set
        {
            RoutingOpts = new();
            value(RoutingOpts);
        }
    }

    /// <summary>
    /// a function to filter out endpoints from auto registration.
    /// return 'false' from the function if you want to exclude an endpoint from registration.
    /// return 'true' to include.
    /// this function will executed for each endpoint that has been discovered during startup.
    /// </summary>
    public Func<EndpointDefinition, bool> EndpointRegistrationFilter { set => EpRegFilterFunc = value; }

    /// <summary>
    /// a function for transforming validation errors to an error response dto.
    /// set it to any func that returns an object that can be serialized to json.
    /// this function will be run everytime an error response needs to be sent to the client.
    /// </summary>
    public Func<IEnumerable<ValidationFailure>, object> ErrorResponseBuilder { set => ErrRespBldrFunc = value; }

    /// <summary>
    /// a function for deserializing the incoming http request body. this function will be executed for each request received if it has json request body.
    /// the parameters of the func are as follows:
    /// <para>HttpRequest: the incoming request</para>
    /// <para>Type: the type of the request dto which the request body will be deserialized into</para>
    /// <para>CancellationToken: a cancellation token</para>
    /// </summary>
    public Func<HttpRequest, Type, CancellationToken, ValueTask<object?>> RequestDeserializer { set => ReqDeserializerFunc = value; }

    /// <summary>
    /// a function for writing serialized response dtos to the response body.
    /// this function will be executed whenever a json response is being sent to the client.
    /// you should set the content-type and write directly to the http response body stream in this function.
    /// the parameters of the func are as follows:
    /// <para>HttpResponse: the http response object</para>
    /// <para>object: the response dto to be serialized</para>
    /// <para>string: the response content-type</para>
    /// <para>CancellationToken: a cancellation token</para>
    /// <code>
    /// config.ResponseSerializer = (rsp, dto, cType, ct) =>
    /// {
    ///     rsp.ContentType = cType;
    ///     return rsp.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(dto), ct);
    /// };
    /// </code>
    /// </summary>
    public Func<HttpResponse, object, string, CancellationToken, Task> ResponseSerializer { set => RespSerializerFunc = value; }
}