using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using static FastEndpoints.Config;

namespace FastEndpoints;

/// <summary>
/// serialization options for the endpoints
/// </summary>
[UnconditionalSuppressMessage("aot", "IL2026"), UnconditionalSuppressMessage("aot", "IL3050")]
public sealed class SerializerOptions
{
    /// <summary>
    /// the json serializer options
    /// </summary>
    public JsonSerializerOptions Options { get; internal set; } = new(); //should only be set from MapFastEndpoints() during startup

    /// <summary>
    /// this is the field name used for adding serializer errors when the serializer throws due to bad json input and the error is not concerning a
    /// particular property/field of the incoming json.
    /// </summary>
    public string SerializerErrorsField { internal get; set; } = "SerializerErrors";

    /// <summary>
    /// the charset used for responses. this will be appended to the content-type header when the <see cref="ResponseSerializer" /> func is used.
    /// defaults to <c>utf-8</c>. set to <c>null</c> to disable appending a charset.
    /// </summary>
    public string? CharacterEncoding { internal get; set; } = "utf-8";

    /// <summary>
    /// a function for deserializing the incoming http request body. this function will be executed for each request received if it has a json request body.
    /// the input parameters of the func are as follows:
    /// <code>
    /// HttpRequest : the incoming request
    /// Type : the type of the request dto which the request body will be deserialized into
    /// JsonSerializerContext? : json serializer context if code generation is used
    /// CancellationToken : a cancellation token
    /// </code>
    /// </summary>
    public Func<HttpRequest, Type, JsonSerializerContext?, CancellationToken, ValueTask<object?>> RequestDeserializer { internal get; set; }
        = (req, tReqDto, jCtx, cancellation)
              => req.ReadFromJsonAsync(
                  type: tReqDto,
                  options: jCtx?.Options ?? SerOpts.Options,
                  cancellationToken: cancellation);

    /// <summary>
    /// a function for writing serialized response dtos to the response body.
    /// this function will be executed whenever a json response is being sent to the client.
    /// you should set the content-type and write directly to the http response body stream in this function.
    /// the parameters of the func are as follows:
    /// <code>
    /// HttpResponse : the http response object.
    /// object : the response dto to be serialized.
    /// string : the response content-type.
    /// JsonSerializerContext? : json serializer context if code generation is used.
    /// CancellationToken : a cancellation token.
    /// </code>
    /// <para>example:</para>
    /// <code>
    /// config.ResponseSerializer = (rsp, dto, cType, jCtx , ct) =>
    /// {
    ///     rsp.ContentType = cType;
    ///     return rsp.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(dto), ct);
    /// };
    /// </code>
    /// </summary>
    public Func<HttpResponse, object?, string, JsonSerializerContext?, CancellationToken, Task> ResponseSerializer { internal get; set; }
        = (rsp, dto, contentType, jCtx, cancellation)
              => dto is null
                     ? Task.CompletedTask
                     : rsp.WriteAsJsonAsync(
                         value: dto,
                         options: jCtx?.Options ?? SerOpts.Options,
                         contentType: SerOpts.CharacterEncoding is null ? contentType : $"{contentType}; charset={SerOpts.CharacterEncoding}",
                         cancellationToken: cancellation);

    /// <summary>
    /// the original json serializer options from the di-registered JsonOptions.
    /// used by IResult types (like Ok&lt;T&gt;) which get their serializer options from di.
    /// </summary>
    internal JsonSerializerOptions? AspNetCoreOptions { get; set; }
}