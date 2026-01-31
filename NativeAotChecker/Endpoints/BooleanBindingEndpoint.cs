using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Test: Boolean binding in various formats in AOT mode
public sealed class BooleanBindingRequest
{
    public bool BoolFromJson { get; set; }
    
    [QueryParam]
    public bool QueryBool { get; set; }
    
    [QueryParam]
    public bool? NullableQueryBool { get; set; }
    
    public List<bool> BoolList { get; set; } = [];
    
    public bool DefaultTrue { get; set; } = true;
    public bool DefaultFalse { get; set; } = false;
}

public sealed class BooleanBindingResponse
{
    public bool BoolFromJson { get; set; }
    public bool QueryBool { get; set; }
    public bool? NullableQueryBool { get; set; }
    public int BoolListCount { get; set; }
    public int TrueCount { get; set; }
    public bool DefaultTrue { get; set; }
    public bool DefaultFalse { get; set; }
    public bool BooleansBound { get; set; }
}

public sealed class BooleanBindingEndpoint : Endpoint<BooleanBindingRequest, BooleanBindingResponse>
{
    public override void Configure()
    {
        Post("boolean-binding-test");
        AllowAnonymous();
        SerializerContext<BooleanBindingSerCtx>();
    }

    public override async Task HandleAsync(BooleanBindingRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new BooleanBindingResponse
        {
            BoolFromJson = req.BoolFromJson,
            QueryBool = req.QueryBool,
            NullableQueryBool = req.NullableQueryBool,
            BoolListCount = req.BoolList.Count,
            TrueCount = req.BoolList.Count(b => b),
            DefaultTrue = req.DefaultTrue,
            DefaultFalse = req.DefaultFalse,
            BooleansBound = true
        }, ct);
    }
}

[JsonSerializable(typeof(BooleanBindingRequest))]
[JsonSerializable(typeof(BooleanBindingResponse))]
public partial class BooleanBindingSerCtx : JsonSerializerContext;
