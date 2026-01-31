using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Various route constraints in AOT mode
public sealed class RouteConstraintRequest
{
    [RouteParam]
    public int IntId { get; set; }

    [RouteParam]
    public Guid GuidId { get; set; }

    [RouteParam]
    public string StringId { get; set; } = string.Empty;

    [QueryParam]
    public double? OptionalDouble { get; set; }

    [QueryParam]
    public bool? OptionalBool { get; set; }
}

public sealed class RouteConstraintResponse
{
    public int IntId { get; set; }
    public Guid GuidId { get; set; }
    public string StringId { get; set; } = string.Empty;
    public double? OptionalDouble { get; set; }
    public bool? OptionalBool { get; set; }
    public bool AllConstraintsMet { get; set; }
}

public sealed class RouteConstraintEndpoint : Endpoint<RouteConstraintRequest, RouteConstraintResponse>
{
    public override void Configure()
    {
        Get("route-constraints/{intId:int}/{guidId:guid}/{stringId}");
        AllowAnonymous();
        SerializerContext<RouteConstraintSerCtx>();
    }

    public override async Task HandleAsync(RouteConstraintRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new RouteConstraintResponse
        {
            IntId = req.IntId,
            GuidId = req.GuidId,
            StringId = req.StringId,
            OptionalDouble = req.OptionalDouble,
            OptionalBool = req.OptionalBool,
            AllConstraintsMet = req.IntId > 0 && req.GuidId != Guid.Empty && !string.IsNullOrEmpty(req.StringId)
        }, ct);
    }
}

// Test: Optional route parameters
public sealed class OptionalRouteRequest
{
    [RouteParam]
    public int RequiredId { get; set; }

    [RouteParam]
    public int? OptionalId { get; set; }
}

public sealed class OptionalRouteResponse
{
    public int RequiredId { get; set; }
    public int? OptionalId { get; set; }
    public bool OptionalWasProvided { get; set; }
}

public sealed class OptionalRouteEndpoint : Endpoint<OptionalRouteRequest, OptionalRouteResponse>
{
    public override void Configure()
    {
        Get("optional-route/{requiredId}/{optionalId?}");
        AllowAnonymous();
        SerializerContext<RouteConstraintSerCtx>();
    }

    public override async Task HandleAsync(OptionalRouteRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new OptionalRouteResponse
        {
            RequiredId = req.RequiredId,
            OptionalId = req.OptionalId,
            OptionalWasProvided = req.OptionalId.HasValue
        }, ct);
    }
}

[JsonSerializable(typeof(RouteConstraintRequest))]
[JsonSerializable(typeof(RouteConstraintResponse))]
[JsonSerializable(typeof(OptionalRouteRequest))]
[JsonSerializable(typeof(OptionalRouteResponse))]
public partial class RouteConstraintSerCtx : JsonSerializerContext;
