using FastEndpoints;
using System.Reflection;

namespace NativeAotChecker.Endpoints;

// Request for reflection tests
public class ReflectionRequest
{
    public string TypeName { get; set; } = string.Empty;
    public string MethodName { get; set; } = string.Empty;
    public string PropertyName { get; set; } = string.Empty;
}

public class ReflectionResponse
{
    public string TypeName { get; set; } = string.Empty;
    public List<string> PropertyNames { get; set; } = [];
    public List<string> MethodNames { get; set; } = [];
    public int PropertyCount { get; set; }
    public int MethodCount { get; set; }
    public bool TypeFound { get; set; }
    public string PropertyValue { get; set; } = string.Empty;
    public bool ReflectionWorked { get; set; }
}

// Test class for reflection
public class ReflectionTestClass
{
    public string Name { get; set; } = "Default";
    public int Value { get; set; } = 42;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    public string GetFullInfo() => $"{Name}: {Value}";
    public int Calculate(int x) => x * Value;
}

/// <summary>
/// Tests reflection-based type inspection in AOT mode.
/// AOT ISSUE: Type.GetType() with string fails without metadata.
/// GetProperties/GetMethods may return incomplete results.
/// Dynamic type loading is not supported in AOT.
/// </summary>
public class ReflectionEndpoint : Endpoint<ReflectionRequest, ReflectionResponse>
{
    public override void Configure()
    {
        Post("reflection-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ReflectionRequest req, CancellationToken ct)
    {
        // Try to get type by name - problematic in AOT
        Type? type = null;
        try
        {
            type = Type.GetType(req.TypeName) ?? typeof(ReflectionTestClass);
        }
        catch
        {
            type = typeof(ReflectionTestClass);
        }

        var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        await Send.OkAsync(new ReflectionResponse
        {
            TypeName = type.FullName ?? type.Name,
            PropertyNames = properties.Select(p => p.Name).ToList(),
            MethodNames = methods.Select(m => m.Name).ToList(),
            PropertyCount = properties.Length,
            MethodCount = methods.Length,
            TypeFound = true,
            ReflectionWorked = properties.Length > 0
        });
    }
}

/// <summary>
/// Tests dynamic instance creation in AOT mode.
/// AOT ISSUE: Activator.CreateInstance uses reflection.
/// Constructor discovery requires metadata preservation.
/// Generic CreateInstance&lt;T&gt; may fail for unregistered types.
/// </summary>
public class DynamicCreationEndpoint : Endpoint<ReflectionRequest, ReflectionResponse>
{
    public override void Configure()
    {
        Post("dynamic-creation-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ReflectionRequest req, CancellationToken ct)
    {
        // Dynamic instance creation - problematic in AOT
        object? instance = null;
        string propertyValue = string.Empty;
        
        try
        {
            instance = Activator.CreateInstance<ReflectionTestClass>();
            
            if (instance != null && !string.IsNullOrEmpty(req.PropertyName))
            {
                var prop = instance.GetType().GetProperty(req.PropertyName);
                propertyValue = prop?.GetValue(instance)?.ToString() ?? "null";
            }
        }
        catch (Exception ex)
        {
            propertyValue = $"Error: {ex.Message}";
        }

        await Send.OkAsync(new ReflectionResponse
        {
            TypeName = instance?.GetType().Name ?? "null",
            PropertyValue = propertyValue,
            ReflectionWorked = instance != null
        });
    }
}

/// <summary>
/// Tests dynamic method invocation in AOT mode.
/// AOT ISSUE: MethodInfo.Invoke uses reflection.
/// Parameter binding at runtime needs metadata.
/// Return value boxing/unboxing uses dynamic dispatch.
/// </summary>
public class DynamicInvocationEndpoint : Endpoint<ReflectionRequest, ReflectionResponse>
{
    public override void Configure()
    {
        Post("dynamic-invocation-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ReflectionRequest req, CancellationToken ct)
    {
        var instance = new ReflectionTestClass { Name = "Test", Value = 10 };
        string result = string.Empty;
        
        try
        {
            var method = instance.GetType().GetMethod(req.MethodName);
            if (method != null)
            {
                var parameters = method.GetParameters();
                object?[] args = parameters.Length > 0 ? [5] : [];
                var returnValue = method.Invoke(instance, args);
                result = returnValue?.ToString() ?? "void";
            }
        }
        catch (Exception ex)
        {
            result = $"Error: {ex.Message}";
        }

        await Send.OkAsync(new ReflectionResponse
        {
            MethodNames = [req.MethodName],
            PropertyValue = result,
            ReflectionWorked = !result.StartsWith("Error")
        });
    }
}
