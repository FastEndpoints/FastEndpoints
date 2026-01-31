using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request for delegate tests
public class DelegateInvokeRequest
{
    public string Operation { get; set; } = string.Empty;
    public int ValueA { get; set; }
    public int ValueB { get; set; }
}

public class DelegateInvokeResponse
{
    public string Operation { get; set; } = string.Empty;
    public int Result { get; set; }
    public bool DelegateInvokeWorked { get; set; }
    public string InvocationMethod { get; set; } = string.Empty;
}

/// <summary>
/// Tests Delegate.DynamicInvoke in AOT mode.
/// AOT ISSUE: DynamicInvoke uses reflection for parameter binding.
/// Late-bound delegate invocation needs runtime type resolution.
/// CreateDelegate with type parameters uses MakeGenericMethod.
/// </summary>
public class DelegateInvokeEndpoint : Endpoint<DelegateInvokeRequest, DelegateInvokeResponse>
{
    public override void Configure()
    {
        Post("delegate-invoke-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(DelegateInvokeRequest req, CancellationToken ct)
    {
        // Create delegate based on operation
        Delegate? operation = req.Operation.ToLowerInvariant() switch
        {
            "add" => new Func<int, int, int>((a, b) => a + b),
            "subtract" => new Func<int, int, int>((a, b) => a - b),
            "multiply" => new Func<int, int, int>((a, b) => a * b),
            "divide" => new Func<int, int, int>((a, b) => b != 0 ? a / b : 0),
            _ => null
        };

        int result = 0;
        string invocationMethod = "none";

        if (operation != null)
        {
            try
            {
                // DynamicInvoke - problematic in AOT
                var dynamicResult = operation.DynamicInvoke(req.ValueA, req.ValueB);
                result = (int)(dynamicResult ?? 0);
                invocationMethod = "DynamicInvoke";
            }
            catch
            {
                // Fallback to direct invocation
                if (operation is Func<int, int, int> func)
                {
                    result = func(req.ValueA, req.ValueB);
                    invocationMethod = "DirectInvoke";
                }
            }
        }

        await Send.OkAsync(new DelegateInvokeResponse
        {
            Operation = req.Operation,
            Result = result,
            DelegateInvokeWorked = invocationMethod == "DynamicInvoke",
            InvocationMethod = invocationMethod
        });
    }
}
