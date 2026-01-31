using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request for generic method tests
public class GenericMethodRequest
{
    public string TypeName { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int IntValue { get; set; }
    public List<string> StringItems { get; set; } = [];
    public List<int> IntItems { get; set; } = [];
}

public class GenericMethodResponse
{
    public string Result { get; set; } = string.Empty;
    public string TypeUsed { get; set; } = string.Empty;
    public int ProcessedCount { get; set; }
    public List<string> ProcessedItems { get; set; } = [];
    public bool GenericMethodWorked { get; set; }
}

/// <summary>
/// Tests generic method invocation via MakeGenericMethod in AOT mode.
/// AOT ISSUE: MakeGenericMethod requires runtime type composition.
/// Generic type arguments at runtime need metadata preservation.
/// Open generic method invocation uses reflection.
/// </summary>
public class GenericMethodEndpoint : Endpoint<GenericMethodRequest, GenericMethodResponse>
{
    public override void Configure()
    {
        Post("generic-method-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GenericMethodRequest req, CancellationToken ct)
    {
        string result;
        string typeUsed;
        
        try
        {
            // Try to use MakeGenericMethod - problematic in AOT
            var method = typeof(GenericMethodEndpoint).GetMethod(nameof(ProcessGeneric));
            
            Type genericType = req.TypeName.ToLowerInvariant() switch
            {
                "int" => typeof(int),
                "string" => typeof(string),
                "double" => typeof(double),
                _ => typeof(object)
            };
            
            var genericMethod = method?.MakeGenericMethod(genericType);
            var processResult = genericMethod?.Invoke(this, [req.Value]);
            
            result = processResult?.ToString() ?? "null";
            typeUsed = genericType.Name;
        }
        catch (Exception ex)
        {
            result = $"Error: {ex.Message}";
            typeUsed = "error";
        }

        await Send.OkAsync(new GenericMethodResponse
        {
            Result = result,
            TypeUsed = typeUsed,
            GenericMethodWorked = !result.StartsWith("Error")
        });
    }

    public string ProcessGeneric<T>(string value)
    {
        return $"Processed as {typeof(T).Name}: {value}";
    }
}

/// <summary>
/// Tests generic type instantiation at runtime in AOT mode.
/// AOT ISSUE: Type.MakeGenericType uses runtime type composition.
/// Generic type cache lookup needs reflection.
/// Closed generic type creation at runtime is not AOT-safe.
/// </summary>
public class GenericTypeEndpoint : Endpoint<GenericMethodRequest, GenericMethodResponse>
{
    public override void Configure()
    {
        Post("generic-type-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(GenericMethodRequest req, CancellationToken ct)
    {
        string result;
        int processedCount = 0;
        
        try
        {
            // Try to create generic type at runtime - problematic in AOT
            Type listType = typeof(List<>);
            Type genericType = req.TypeName.ToLowerInvariant() switch
            {
                "int" => typeof(int),
                "string" => typeof(string),
                _ => typeof(object)
            };
            
            Type constructedType = listType.MakeGenericType(genericType);
            object? instance = Activator.CreateInstance(constructedType);
            
            // Try to add items
            var addMethod = constructedType.GetMethod("Add");
            if (instance != null && addMethod != null)
            {
                foreach (var item in req.StringItems)
                {
                    try
                    {
                        object? converted = genericType == typeof(int) 
                            ? int.Parse(item) 
                            : (object)item;
                        addMethod.Invoke(instance, [converted]);
                        processedCount++;
                    }
                    catch { }
                }
            }
            
            result = $"Created List<{genericType.Name}> with {processedCount} items";
        }
        catch (Exception ex)
        {
            result = $"Error: {ex.Message}";
        }

        await Send.OkAsync(new GenericMethodResponse
        {
            Result = result,
            ProcessedCount = processedCount,
            GenericMethodWorked = processedCount > 0
        });
    }
}
