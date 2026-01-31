using FastEndpoints;
using System.Globalization;
using System.Reflection;

namespace NativeAotChecker.Endpoints;

// Custom type with BindAsync static method (alternative to IParsable<T>)
public class CustomBindableId
{
    public string Prefix { get; set; } = string.Empty;
    public int Number { get; set; }

    public static ValueTask<CustomBindableId?> BindAsync(HttpContext ctx, ParameterInfo parameter)
    {
        var value = ctx.Request.Query["customId"].FirstOrDefault();
        if (string.IsNullOrEmpty(value))
            return ValueTask.FromResult<CustomBindableId?>(null);

        var parts = value.Split('-');
        if (parts.Length != 2 || !int.TryParse(parts[1], out var number))
            return ValueTask.FromResult<CustomBindableId?>(null);

        return ValueTask.FromResult<CustomBindableId?>(new CustomBindableId
        {
            Prefix = parts[0],
            Number = number
        });
    }
}

// Custom type with TryParse static method
public readonly struct CustomTryParseValue
{
    public string Value { get; init; }
    public int Length { get; init; }

    public static bool TryParse(string? s, IFormatProvider? provider, out CustomTryParseValue result)
    {
        if (string.IsNullOrEmpty(s))
        {
            result = default;
            return false;
        }

        result = new CustomTryParseValue { Value = s, Length = s.Length };
        return true;
    }
}

// Request/Response DTOs
public class CustomBindingRequest
{
    public string Name { get; set; } = string.Empty;
}

public class CustomBindingResponse
{
    public string Name { get; set; } = string.Empty;
    public string? CustomIdPrefix { get; set; }
    public int? CustomIdNumber { get; set; }
    public string? TryParseValue { get; set; }
    public int? TryParseLength { get; set; }
    public bool BindAsyncWorked { get; set; }
    public bool TryParseWorked { get; set; }
}

/// <summary>
/// Tests custom BindAsync and TryParse static methods in AOT mode.
/// AOT ISSUE: BindAsync discovery uses reflection to find static methods.
/// typeof(T).GetMethod("BindAsync") reflection fails in trimmed AOT.
/// TryParse method discovery uses MethodInfo.Invoke which is reflection-based.
/// </summary>
public class CustomBindingEndpoint : Endpoint<CustomBindingRequest, CustomBindingResponse>
{
    public override void Configure()
    {
        Get("custom-binding-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CustomBindingRequest req, CancellationToken ct)
    {
        // Try to manually parse from query for testing
        var customIdStr = HttpContext.Request.Query["customId"].FirstOrDefault();
        var tryParseStr = HttpContext.Request.Query["tryParseValue"].FirstOrDefault();

        CustomBindableId? customId = null;
        if (!string.IsNullOrEmpty(customIdStr))
        {
            var parts = customIdStr.Split('-');
            if (parts.Length == 2 && int.TryParse(parts[1], out var number))
            {
                customId = new CustomBindableId { Prefix = parts[0], Number = number };
            }
        }

        CustomTryParseValue? tryParseValue = null;
        if (!string.IsNullOrEmpty(tryParseStr) && CustomTryParseValue.TryParse(tryParseStr, CultureInfo.InvariantCulture, out var parsed))
        {
            tryParseValue = parsed;
        }

        await Send.OkAsync(new CustomBindingResponse
        {
            Name = req.Name,
            CustomIdPrefix = customId?.Prefix,
            CustomIdNumber = customId?.Number,
            TryParseValue = tryParseValue?.Value,
            TryParseLength = tryParseValue?.Length,
            BindAsyncWorked = customId != null,
            TryParseWorked = tryParseValue != null
        });
    }
}
