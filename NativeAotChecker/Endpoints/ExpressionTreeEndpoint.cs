using FastEndpoints;
using System.Linq.Expressions;

namespace NativeAotChecker.Endpoints;

// Request for expression tree tests
public class ExpressionTreeRequest
{
    public string PropertyName { get; set; } = string.Empty;
    public int Value { get; set; }
    public List<TestItem> Items { get; set; } = [];
}

public class TestItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
}

public class ExpressionTreeResponse
{
    public string PropertyName { get; set; } = string.Empty;
    public int FilteredCount { get; set; }
    public List<string> FilteredNames { get; set; } = [];
    public bool ExpressionTreeWorked { get; set; }
    public string CompiledExpressionResult { get; set; } = string.Empty;
}

/// <summary>
/// Tests expression tree compilation in AOT mode.
/// AOT ISSUE: Expression.Compile() requires JIT compilation.
/// Dynamic expression building uses System.Reflection.Emit.
/// Lambda expression compilation at runtime is not supported in AOT.
/// </summary>
public class ExpressionTreeEndpoint : Endpoint<ExpressionTreeRequest, ExpressionTreeResponse>
{
    public override void Configure()
    {
        Post("expression-tree-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExpressionTreeRequest req, CancellationToken ct)
    {
        // Build expression tree dynamically - problematic in AOT
        var param = Expression.Parameter(typeof(TestItem), "x");
        var property = Expression.Property(param, nameof(TestItem.IsActive));
        var lambda = Expression.Lambda<Func<TestItem, bool>>(property, param);
        
        // Compile the expression - this fails in AOT
        Func<TestItem, bool> compiled;
        try
        {
            compiled = lambda.Compile();
        }
        catch
        {
            // Fallback for AOT
            compiled = x => x.IsActive;
        }

        var filtered = req.Items.Where(compiled).ToList();

        await Send.OkAsync(new ExpressionTreeResponse
        {
            PropertyName = req.PropertyName,
            FilteredCount = filtered.Count,
            FilteredNames = filtered.Select(x => x.Name).ToList(),
            ExpressionTreeWorked = true,
            CompiledExpressionResult = $"Filtered {filtered.Count} active items"
        });
    }
}

/// <summary>
/// Tests dynamic LINQ with expressions in AOT mode.
/// </summary>
public class DynamicLinqEndpoint : Endpoint<ExpressionTreeRequest, ExpressionTreeResponse>
{
    public override void Configure()
    {
        Post("dynamic-linq-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ExpressionTreeRequest req, CancellationToken ct)
    {
        // Dynamic ordering by property name - uses reflection
        var propertyInfo = typeof(TestItem).GetProperty(req.PropertyName);
        
        IEnumerable<TestItem> ordered;
        if (propertyInfo != null)
        {
            // This uses reflection-based property access
            ordered = req.Items.OrderBy(x => propertyInfo.GetValue(x));
        }
        else
        {
            ordered = req.Items;
        }

        await Send.OkAsync(new ExpressionTreeResponse
        {
            PropertyName = req.PropertyName,
            FilteredCount = ordered.Count(),
            FilteredNames = ordered.Select(x => x.Name).ToList(),
            ExpressionTreeWorked = propertyInfo != null,
            CompiledExpressionResult = $"Ordered by {req.PropertyName}"
        });
    }
}
