using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using static FastEndpoints.Config;

namespace FastEndpoints;

/// <summary>
/// serialization options for the endpoints
/// </summary>
public class SerializerOptions
{
    /// <summary>
    /// the json serializer options
    /// </summary>
    public JsonSerializerOptions Options { get; internal set; } = new(); //should only be set from UseFastEndpoints() during startup

    /// <summary>
    /// a function for deserializing the incoming http request body. this function will be executed for each request received if it has a json request body.
    /// the parameters of the func are as follows:
    /// <para>HttpRequest: the incoming request</para>
    /// <para>Type: the type of the request dto which the request body will be deserialized into</para>
    /// <para>JsonSerializerContext?: json serializer context if code generation is used</para>
    /// <para>CancellationToken: a cancellation token</para>
    /// </summary>
    public Func<HttpRequest, Type, JsonSerializerContext?, CancellationToken, ValueTask<object?>> RequestDeserializer { internal get; set; }
        = (req, tReqDto, jCtx, cancellation) =>
        {
            return jCtx == null
               ? JsonSerializer.DeserializeAsync(req.Body, tReqDto, SerOpts.Options, cancellation)
               : JsonSerializer.DeserializeAsync(req.Body, tReqDto, jCtx, cancellation);
        };

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
    public Func<HttpResponse, object?, string, JsonSerializerContext?, CancellationToken, Task> ResponseSerializer { internal get; set; }
        = (rsp, dto, contentType, jCtx, cancellation) =>
        {
            rsp.ContentType = contentType;

            if (dto is null)
                return Task.CompletedTask;

            return jCtx == null
                   ? JsonSerializer.SerializeAsync(rsp.Body, dto, dto.GetType(), SerOpts.Options, cancellation)
                   : JsonSerializer.SerializeAsync(rsp.Body, dto, dto.GetType(), jCtx, cancellation);
        };
}