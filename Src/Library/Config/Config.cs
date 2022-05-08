using FluentValidation.Results;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;

#pragma warning disable CA1822, IDE1006

namespace FastEndpoints;
/// <summary>
/// global configuration settings for FastEndpoints
/// </summary>
public class Config
{
    internal static JsonSerializerOptions SerializerOpts { get; set; } = new(); //should only be set from UseFastEndpoints() during startup
    internal static bool ShortEpNames { get; private set; }
    internal static VersioningOptions? VersioningOpts { get; private set; }
    internal static ThrottleOptions? ThrottleOpts { get; private set; }
    internal static RoutingOptions? RoutingOpts { get; private set; }
    internal static Func<EndpointDefinition, bool>? EpRegFilterFunc { get; private set; }
    internal static Action<EndpointDefinition, RouteHandlerBuilder>? GlobalEpOptsAction { get; private set; }
    internal static Func<List<ValidationFailure>, int, object> ErrRespBldrFunc { get; private set; }
        = (failures, statusCode) => new ErrorResponse(failures, statusCode);
    internal static int ErrRespStatusCode { get; private set; } = 400;
    internal static Func<HttpRequest, Type, JsonSerializerContext?, CancellationToken, ValueTask<object?>> ReqDeserializerFunc { get; private set; }
        = (req, tReqDto, jCtx, cancellation) =>
        {
            return jCtx == null
               ? JsonSerializer.DeserializeAsync(req.Body, tReqDto, SerializerOpts, cancellation)
               : JsonSerializer.DeserializeAsync(req.Body, tReqDto, jCtx, cancellation);
        };
    internal static Func<HttpResponse, object, string, JsonSerializerContext?, CancellationToken, Task> RespSerializerFunc { get; private set; }
        = (rsp, dto, contentType, jCtx, cancellation) =>
        {
            rsp.ContentType = contentType;
            return jCtx == null
                   ? JsonSerializer.SerializeAsync(rsp.Body, dto, dto.GetType(), SerializerOpts, cancellation)
                   : JsonSerializer.SerializeAsync(rsp.Body, dto, dto.GetType(), jCtx, cancellation);
        };

    /// <summary>
    /// set to true if you'd like the endpoint names/ swagger operation ids to be just the endpoint class names instead of the full names including namespace.
    /// </summary>
    public bool ShortEndpointNames { set => ShortEpNames = value; }

    /// <summary>
    /// settings for configuring the json serializer
    /// </summary>
    public Action<JsonSerializerOptions> SerializerOptions { set => value(SerializerOpts); }

    /// <summary>
    /// options for enabling endpoint versioning support
    /// </summary>
    public Action<VersioningOptions> VersioningOptions {
        set {
            VersioningOpts = new();
            value(VersioningOpts);
        }
    }

    /// <summary>
    /// routing options for all endpoints
    /// </summary>
    public Action<RoutingOptions> RoutingOptions {
        set {
            RoutingOpts = new();
            value(RoutingOpts);
        }
    }

    /// <summary>
    /// throttling options for all endpoints
    /// </summary>
    public Action<ThrottleOptions> ThrottleOptions {
        set {
            ThrottleOpts = new();
            value(ThrottleOpts);
        }
    }

    /// <summary>
    /// a function to filter out endpoints from auto registration.
    /// the function you set here will be executed for each endpoint during startup.
    /// you can inspect the EndpointSettings to check what the current endpoint is, if needed.
    /// return 'false' from the function if you want to exclude an endpoint from registration.
    /// return 'true' to include.
    /// this function will executed for each endpoint that has been discovered during startup.
    /// </summary>
    public Func<EndpointDefinition, bool> EndpointRegistrationFilter { set => EpRegFilterFunc = value; }

    /// <summary>
    /// an action to be performed on all endpoints during registration.
    /// the action you set here will be executed for each endpoint during startup.
    /// you can inspect the EndpointSettings to check what the current endpoint is, if needed.
    /// NOTE: this action is executed before Options() and Describe() of each individual endpoint.
    /// so, whatever you do here may get overridden or compounded by what you do in the Configure() method of each endpoint.
    /// </summary>
    public Action<EndpointDefinition, RouteHandlerBuilder> GlobalEndpointOptions { set => GlobalEpOptsAction = value; }

    /// <summary>
    /// this http status code will be used for all automatically sent validation failure responses. defaults to 400.
    /// </summary>
    public int ErrorResponseStatusCode { set => ErrRespStatusCode = value; }

    /// <summary>
    /// a function for transforming validation errors to an error response dto.
    /// set it to any func that returns an object that can be serialized to json.
    /// this function will be run everytime an error response needs to be sent to the client.
    /// the arguments for the func will be a collection of validation failures and an http status code.
    /// </summary>
    public Func<IEnumerable<ValidationFailure>, int, object> ErrorResponseBuilder { set => ErrRespBldrFunc = value; }

    /// <summary>
    /// a function for deserializing the incoming http request body. this function will be executed for each request received if it has a json request body.
    /// the parameters of the func are as follows:
    /// <para>HttpRequest: the incoming request</para>
    /// <para>Type: the type of the request dto which the request body will be deserialized into</para>
    /// <para>JsonSerializerContext?: json serializer context if code generation is used</para>
    /// <para>CancellationToken: a cancellation token</para>
    /// </summary>
    public Func<HttpRequest, Type, JsonSerializerContext?, CancellationToken, ValueTask<object?>> RequestDeserializer { set => ReqDeserializerFunc = value; }

    /// <summary>
    /// a function for writing serialized response dtos to the response body.
    /// this function will be executed whenever a json response is being sent to the client.
    /// you should set the content-type and write directly to the http response body stream in this function.
    /// the parameters of the func are as follows:
    /// <para>HttpResponse: the http response object</para>
    /// <para>object: the response dto to be serialized</para>
    /// <para>string: the response content-type</para>
    /// <para>JsonSerializerContext?: json serializer context if code generation is used</para>
    /// <para>CancellationToken: a cancellation token</para>
    /// <code>
    /// config.ResponseSerializer = (rsp, dto, cType, jCtx , ct) =>
    /// {
    ///     rsp.ContentType = cType;
    ///     return rsp.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(dto), ct);
    /// };
    /// </code>
    /// </summary>
    public Func<HttpResponse, object, string, JsonSerializerContext?, CancellationToken, Task> ResponseSerializer { set => RespSerializerFunc = value; }
}