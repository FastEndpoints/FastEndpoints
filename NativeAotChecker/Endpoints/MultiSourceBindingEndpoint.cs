using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

public sealed class MultiSourceBindingRequest
{
    public string Description { get; set; }

    [RouteParam]
    public int Id { get; set; }

    [QueryParam]
    public string Category { get; set; }

    [FromClaim("user-id")]
    public string UserId { get; set; }

    [FormField]
    public string FormValue { get; set; }

    [FromHeader("x-request-id")]
    public string RequestId { get; set; }
}

public sealed class MultiSourceBindingResponse
{
    public string Description { get; set; }
    public int Id { get; set; }
    public string Category { get; set; }
    public string UserId { get; set; }
    public string FormValue { get; set; }
    public string RequestId { get; set; }
}

public sealed class MultiSourceBindingEndpoint : Endpoint<MultiSourceBindingRequest, MultiSourceBindingResponse>
{
    public override void Configure()
    {
        Post("multi-source/{id}");
        AllowFormData();
        SerializerContext<MultiSourceBindingSerCtx>();
    }

    public override async Task HandleAsync(MultiSourceBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(
            new()
            {
                Description = req.Description,
                Id = req.Id,
                Category = req.Category,
                UserId = req.UserId,
                FormValue = req.FormValue,
                RequestId = req.RequestId
            },
            ct);
    }
}

[JsonSerializable(typeof(MultiSourceBindingResponse))]
public partial class MultiSourceBindingSerCtx : JsonSerializerContext;