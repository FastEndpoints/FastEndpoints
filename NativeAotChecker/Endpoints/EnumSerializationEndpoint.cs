using FastEndpoints;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Enum with EnumMember attribute for custom serialization names
[JsonConverter(typeof(JsonStringEnumConverter<StatusWithCustomNames>))]
public enum StatusWithCustomNames
{
    [EnumMember(Value = "pending_status")]
    Pending,
    
    [EnumMember(Value = "active_status")]
    Active,
    
    [EnumMember(Value = "completed_status")]
    Completed,
    
    [EnumMember(Value = "cancelled_status")]
    Cancelled
}

// Flags enum
[Flags]
[JsonConverter(typeof(JsonStringEnumConverter<PermissionFlags>))]
public enum PermissionFlags
{
    None = 0,
    Read = 1,
    Write = 2,
    Delete = 4,
    Admin = 8,
    All = Read | Write | Delete | Admin
}

// Request
public class EnumSerializationRequest
{
    public StatusWithCustomNames Status { get; set; }
    public PermissionFlags Permissions { get; set; }
    public List<StatusWithCustomNames> StatusList { get; set; } = [];
}

// Response
public class EnumSerializationResponse
{
    public StatusWithCustomNames Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public PermissionFlags Permissions { get; set; }
    public string PermissionsDisplay { get; set; } = string.Empty;
    public int StatusListCount { get; set; }
    public bool HasReadPermission { get; set; }
    public bool HasWritePermission { get; set; }
    public bool EnumSerializationWorked { get; set; }
}

/// <summary>
/// Tests enum with [EnumMember] and [Flags] attributes in AOT mode.
/// AOT ISSUE: EnumMember attribute discovery uses reflection.
/// JsonStringEnumConverter needs enum metadata preserved.
/// Flags enum combination parsing uses Enum.HasFlag which may use reflection.
/// </summary>
public class EnumSerializationEndpoint : Endpoint<EnumSerializationRequest, EnumSerializationResponse>
{
    public override void Configure()
    {
        Post("enum-serialization-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(EnumSerializationRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new EnumSerializationResponse
        {
            Status = req.Status,
            StatusName = req.Status.ToString(),
            Permissions = req.Permissions,
            PermissionsDisplay = req.Permissions.ToString(),
            StatusListCount = req.StatusList.Count,
            HasReadPermission = req.Permissions.HasFlag(PermissionFlags.Read),
            HasWritePermission = req.Permissions.HasFlag(PermissionFlags.Write),
            EnumSerializationWorked = true
        });
    }
}
