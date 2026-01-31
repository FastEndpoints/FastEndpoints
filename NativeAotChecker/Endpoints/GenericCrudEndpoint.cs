using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Base class for generic endpoint
public abstract class GenericCrudBase<TEntity, TId> : EndpointWithoutRequest<CrudResponse<TEntity>>
    where TEntity : class, new()
{
    protected abstract TEntity GetEntity(TId id);
    protected abstract TId ParseId(string idString);
}

public class CrudResponse<T>
{
    public T? Entity { get; set; }
    public string EntityTypeName { get; set; } = string.Empty;
    public bool Success { get; set; }
}

// Entity class
public class ProductEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Tests generic base class with constraints in AOT mode.
/// AOT ISSUE: Generic class with 'where TEntity : class, new()' constraint.
/// new() constraint requires Activator.CreateInstance which is reflection-based.
/// Generic type resolution for base class uses MakeGenericType.
/// </summary>
public class GenericCrudEndpoint : GenericCrudBase<ProductEntity, int>
{
    public override void Configure()
    {
        Get("generic-crud/{id}");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var idString = Route<string>("id") ?? "0";
        var id = ParseId(idString);
        var entity = GetEntity(id);

        await Send.OkAsync(new CrudResponse<ProductEntity>
        {
            Entity = entity,
            EntityTypeName = typeof(ProductEntity).Name,
            Success = true
        });
    }

    protected override ProductEntity GetEntity(int id)
    {
        return new ProductEntity
        {
            Id = id,
            Name = $"Product {id}",
            Price = id * 10.99m,
            Category = "Test Category"
        };
    }

    protected override int ParseId(string idString)
    {
        return int.TryParse(idString, out var id) ? id : 0;
    }
}
