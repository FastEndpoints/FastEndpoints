using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

/// <summary>
/// Test endpoint for generic command handlers in AOT mode.
/// Without PR2 (Command Handler AOT support), this endpoint will fail because:
/// - InitGenericHandler uses MakeGenericType() to create closed generic handler types
/// - MakeGenericType() fails in Native AOT without proper factory generation
/// </summary>
sealed class GenericCommandEndpoint : EndpointWithoutRequest<GenericCommandResponse>
{
    public override void Configure()
    {
        Get("generic-command");
        AllowAnonymous();
        SerializerContext<GenericCommandSerCtx>();
    }

    public override async Task<GenericCommandResponse> ExecuteAsync(CancellationToken ct)
    {
        // Execute a generic command - this uses the generic command handler mechanism
        // which relies on MakeGenericType in InitGenericHandler
        var stringCmd = new GenericWrapperCommand<string> { Value = "Hello AOT" };
        var stringResult = await stringCmd.ExecuteAsync(ct);

        var intCmd = new GenericWrapperCommand<int> { Value = 42 };
        var intResult = await intCmd.ExecuteAsync(ct);

        return new GenericCommandResponse
        {
            StringResult = stringResult,
            IntResult = intResult
        };
    }
}

public sealed class GenericCommandResponse
{
    public string StringResult { get; set; } = "";
    public string IntResult { get; set; } = "";
}

/// <summary>
/// A generic command that wraps a value and returns a formatted string.
/// This is an open generic command type.
/// </summary>
sealed class GenericWrapperCommand<T> : ICommand<string>
{
    public T Value { get; set; } = default!;
}

/// <summary>
/// Generic command handler that handles any GenericWrapperCommand{T}.
/// In AOT mode, this handler needs source-generated factories to work.
/// </summary>
sealed class GenericWrapperCommandHandler<T> : ICommandHandler<GenericWrapperCommand<T>, string>
{
    public Task<string> ExecuteAsync(GenericWrapperCommand<T> cmd, CancellationToken ct)
    {
        var typeName = typeof(T).Name;
        return Task.FromResult($"Wrapped {typeName}: {cmd.Value}");
    }
}

[JsonSerializable(typeof(GenericCommandResponse))]
public partial class GenericCommandSerCtx : JsonSerializerContext;
