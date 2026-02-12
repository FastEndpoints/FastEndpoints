namespace NativeAotChecker.Endpoints.Commands;

public sealed class AotGenericCommand<TData> : ICommand<AotGenericResult<TData>> where TData : class, new()
{
    public string OperationType { get; set; } = string.Empty;
    public TData Data { get; set; } = new();
}

public sealed class AotGenericResult<TData>
{
    public bool Success { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public TData? Data { get; set; }
    public string HandlerType { get; set; } = string.Empty;
}

public sealed class AotGenericCommandHandler<TData> : ICommandHandler<AotGenericCommand<TData>, AotGenericResult<TData>> where TData : class, new()
{
    public Task<AotGenericResult<TData>> ExecuteAsync(AotGenericCommand<TData> cmd, CancellationToken ct)
        => Task.FromResult(
            new AotGenericResult<TData>
            {
                Success = true,
                OperationType = cmd.OperationType,
                Data = cmd.Data,
                HandlerType = $"AotGenericCommandHandler<{typeof(TData).Name}>"
            });
}

public sealed class ProductData
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

public sealed class OrderData
{
    public int OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
}

public sealed class GenericCommandRequest
{
    public string OperationType { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
}

public sealed class GenericCommandResponse
{
    public bool Success { get; set; }
    public string OperationType { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public decimal ProductPrice { get; set; }
    public string HandlerType { get; set; } = string.Empty;
}

public sealed class GenericCommandEndpoint : Endpoint<GenericCommandRequest, GenericCommandResponse>
{
    public override void Configure()
    {
        Post("generic-command");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GenericCommandRequest req, CancellationToken ct)
    {
        var cmd = new AotGenericCommand<ProductData>
        {
            OperationType = req.OperationType,
            Data = new()
            {
                Name = req.ProductName,
                Price = req.ProductPrice
            }
        };

        var result = await cmd.ExecuteAsync(ct);

        await Send.OkAsync(
            new()
            {
                Success = result.Success,
                OperationType = result.OperationType,
                ProductName = result.Data?.Name ?? "",
                ProductPrice = result.Data?.Price ?? 0,
                HandlerType = result.HandlerType
            },
            ct);
    }
}