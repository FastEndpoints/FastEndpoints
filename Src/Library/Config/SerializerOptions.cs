﻿using Microsoft.AspNetCore.Http;
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
    public JsonSerializerOptions Options { get; internal set; } = new(); //should only be set from MapFastEndpoints() during startup

    /// <summary>
    /// a function for deserializing the incoming http request body. this function will be executed for each request received if it has a json request body.
    /// the input parameters of the func are as follows:
    /// <para><see cref="HttpRequest"/> : the incoming request</para>
    /// <para><see cref="Type"/> : the type of the request dto which the request body will be deserialized into</para>
    /// <para><see cref="JsonSerializerContext"/>? : json serializer context if code generation is used</para>
    /// <para><see cref="CancellationToken"/> : a cancellation token</para>
    /// </summary>
    public Func<HttpRequest, Type, JsonSerializerContext?, CancellationToken, ValueTask<object?>> RequestDeserializer { internal get; set; }
        = (req, tReqDto, jCtx, cancellation) =>
        {
            return req.ReadFromJsonAsync(tReqDto, jCtx?.Options ?? SerOpts.Options, cancellation);
        };

    /// <summary>
    /// a function for writing serialized response dtos to the response body.
    /// this function will be executed whenever a json response is being sent to the client.
    /// you should set the content-type and write directly to the http response body stream in this function.
    /// the parameters of the func are as follows:
    /// <para><see cref="HttpResponse"/> : the http response object</para>
    /// <para><see cref="object"/> : the response dto to be serialized</para>
    /// <para><see cref="string"/> : the response content-type</para>
    /// <para><see cref="JsonSerializerContext"/>? : json serializer context if code generation is used</para>
    /// <para><see cref="CancellationToken"/> : a cancellation token</para>
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
        = (rsp, dto, contentType, jCtx, cancellation) =>
        {
            rsp.ContentType = contentType;

            if (dto is null)
                return Task.CompletedTask;

            return rsp.WriteAsJsonAsync(dto, dto.GetType(), jCtx?.Options ?? SerOpts.Options, cancellation);
        };
}