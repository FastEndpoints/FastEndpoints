using FastEndpoints;
using System.Text.Json.Serialization;

namespace NativeAotChecker.Endpoints;

// Types for pattern matching
[JsonPolymorphic(TypeDiscriminatorPropertyName = "shapeType")]
[JsonDerivedType(typeof(CircleShape), "circle")]
[JsonDerivedType(typeof(RectangleShape), "rectangle")]
[JsonDerivedType(typeof(TriangleShape), "triangle")]
public abstract class Shape
{
    public string Color { get; set; } = string.Empty;
    public abstract double CalculateArea();
}

public class CircleShape : Shape
{
    public double Radius { get; set; }
    public override double CalculateArea() => Math.PI * Radius * Radius;
}

public class RectangleShape : Shape
{
    public double Width { get; set; }
    public double Height { get; set; }
    public override double CalculateArea() => Width * Height;
}

public class TriangleShape : Shape
{
    public double Base { get; set; }
    public double Height { get; set; }
    public override double CalculateArea() => 0.5 * Base * Height;
}

// Request/Response
public class PatternMatchingRequest
{
    public Shape? Shape { get; set; }
    public List<Shape> Shapes { get; set; } = [];
}

public class PatternMatchingResponse
{
    public string ShapeType { get; set; } = string.Empty;
    public double Area { get; set; }
    public string MatchResult { get; set; } = string.Empty;
    public Dictionary<string, int> ShapeCounts { get; set; } = [];
    public bool PatternMatchingWorked { get; set; }
}

/// <summary>
/// Tests type pattern matching in AOT mode.
/// AOT ISSUE: Type patterns use 'is' and 'as' which need runtime type checks.
/// Switch expressions on types use type metadata.
/// Polymorphic serialization combined with pattern matching is complex.
/// </summary>
public class PatternMatchingEndpoint : Endpoint<PatternMatchingRequest, PatternMatchingResponse>
{
    public override void Configure()
    {
        Post("pattern-matching-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(PatternMatchingRequest req, CancellationToken ct)
    {
        var matchResult = req.Shape switch
        {
            CircleShape c => $"Circle with radius {c.Radius}",
            RectangleShape r => $"Rectangle {r.Width}x{r.Height}",
            TriangleShape t => $"Triangle base {t.Base} height {t.Height}",
            null => "No shape provided",
            _ => "Unknown shape"
        };

        // Count shapes by type using pattern matching
        var shapeCounts = new Dictionary<string, int>
        {
            ["circles"] = req.Shapes.Count(s => s is CircleShape),
            ["rectangles"] = req.Shapes.Count(s => s is RectangleShape),
            ["triangles"] = req.Shapes.Count(s => s is TriangleShape)
        };

        await Send.OkAsync(new PatternMatchingResponse
        {
            ShapeType = req.Shape?.GetType().Name ?? "none",
            Area = req.Shape?.CalculateArea() ?? 0,
            MatchResult = matchResult,
            ShapeCounts = shapeCounts,
            PatternMatchingWorked = req.Shape != null
        });
    }
}
