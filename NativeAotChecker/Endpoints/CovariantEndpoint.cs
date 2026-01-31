using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Covariant interface
public interface IReadOnlyRepository<out T>
{
    T? GetById(int id);
    IEnumerable<T> GetAll();
}

// Contravariant interface  
public interface IComparer<in T>
{
    int Compare(T? x, T? y);
}

// Base and derived entities
public class BaseEntity
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class DerivedEntity : BaseEntity
{
    public string ExtraField { get; set; } = string.Empty;
}

// Request/Response
public class CovariantRequest
{
    public List<BaseEntity> Entities { get; set; } = [];
    public int EntityId { get; set; }
}

public class CovariantResponse
{
    public string EntityType { get; set; } = string.Empty;
    public int EntityCount { get; set; }
    public bool CovariantWorked { get; set; }
    public List<string> EntityNames { get; set; } = [];
}

/// <summary>
/// Tests covariant/contravariant generic interfaces in AOT mode.
/// AOT ISSUE: Variance handling requires runtime type checking.
/// IEnumerable&lt;Derived&gt; to IEnumerable&lt;Base&gt; conversion uses reflection.
/// Generic interface dispatch with variance needs runtime resolution.
/// </summary>
public class CovariantEndpoint : Endpoint<CovariantRequest, CovariantResponse>
{
    public override void Configure()
    {
        Post("covariant-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(CovariantRequest req, CancellationToken ct)
    {
        // Test covariant assignment
        IEnumerable<BaseEntity> baseEntities = req.Entities;
        
        // Test runtime type checking
        var entityTypes = req.Entities
            .Select(e => e.GetType().Name)
            .Distinct()
            .ToList();

        await Send.OkAsync(new CovariantResponse
        {
            EntityType = entityTypes.FirstOrDefault() ?? "unknown",
            EntityCount = req.Entities.Count,
            EntityNames = req.Entities.Select(e => e.Name).ToList(),
            CovariantWorked = req.Entities.Count > 0
        });
    }
}
