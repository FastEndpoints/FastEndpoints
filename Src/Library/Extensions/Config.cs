using FastEndpoints.Validation.Results;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

#pragma warning disable CA1822, IDE1006

namespace FastEndpoints;
/// <summary>
/// global configuration settings for FastEndpoints
/// </summary>
public class Config
{
    internal static JsonSerializerOptions serializerOptions { get; set; } = new(); //should only be set from UseFastEndpoints() during startup

    internal static Func<DiscoveredEndpoint, bool>? endpointRegistrationFilter { get; private set; }

    internal static Func<IEnumerable<ValidationFailure>, object> errorResponseBuilder { get; private set; }
        = failures => new ErrorResponse(failures);

    internal static Func<HttpRequest, Type, CancellationToken, ValueTask<object?>> requestDeserializer { get; private set; }
        = (req, tReqDto, cancellation) => req.ReadFromJsonAsync(tReqDto, serializerOptions, cancellation);

    internal static Func<HttpResponse, object, string?, CancellationToken, Task> responseSerializer { get; private set; }
        = (rsp, dto, contentType, cancellation)
            => contentType is null
               ? rsp.WriteAsJsonAsync(dto, cancellation)
               : rsp.WriteAsJsonAsync(dto, serializerOptions, contentType, cancellation);

    /// <summary>
    /// settings for configuring the json serializer
    /// </summary>
    public Action<JsonSerializerOptions>? SerializerOptions { set => value?.Invoke(serializerOptions); }

    /// <summary>
    /// a function to filter out endpoints from auto registration.
    /// return 'false' from the function if you want to exclude an endpoint from registration.
    /// return 'true' to include.
    /// this function will executed for each endpoint that has been discovered during startup.
    /// </summary>
    public Func<DiscoveredEndpoint, bool> EndpointRegistrationFilter { set => endpointRegistrationFilter = value; }

    /// <summary>
    /// a function for transforming validation errors to an error response dto.
    /// set it to any func that returns an object that can be serialized to json.
    /// this function will be run everytime an error response needs to be sent to the client.
    /// </summary>
    public Func<IEnumerable<ValidationFailure>, object> ErrorResponseBuilder { set => errorResponseBuilder = value; }

    /// <summary>
    /// a function for deserializing the incoming http request body. this function will be executed for each request received if it has json request body.
    /// the parameters of the func are as follows:
    /// <para>HttpRequest: the incoming request</para>
    /// <para>Type: the type of the request dto which the request body will be deserialized into</para>
    /// <para>CancellationToken: a cancellation token</para>
    /// </summary>
    public Func<HttpRequest, Type, CancellationToken, ValueTask<object?>> RequestDeserializer { set => requestDeserializer = value; }
}
