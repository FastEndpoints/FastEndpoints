using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Request with ValueTuple properties
public class ValueTupleRequest
{
    public (string Name, int Age) Person { get; set; }
    public (int X, int Y, int Z) Coordinates { get; set; }
    public (string, string, string) UnnamedTuple { get; set; }
    public List<(int Id, string Value)> TupleList { get; set; } = [];
}

public class ValueTupleResponse
{
    public string PersonName { get; set; } = string.Empty;
    public int PersonAge { get; set; }
    public int CoordinateSum { get; set; }
    public int TupleListCount { get; set; }
    public bool ValueTupleWorked { get; set; }
}

/// <summary>
/// Tests ValueTuple serialization in AOT mode.
/// AOT ISSUE: ValueTuple generic types need source generation for each arity.
/// Named tuple element names are stored in TupleElementNamesAttribute.
/// Runtime tuple creation uses Activator.CreateInstance.
/// </summary>
public class ValueTupleEndpoint : Endpoint<ValueTupleRequest, ValueTupleResponse>
{
    public override void Configure()
    {
        Post("value-tuple-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(ValueTupleRequest req, CancellationToken ct)
    {
        await Send.OkAsync(new ValueTupleResponse
        {
            PersonName = req.Person.Name,
            PersonAge = req.Person.Age,
            CoordinateSum = req.Coordinates.X + req.Coordinates.Y + req.Coordinates.Z,
            TupleListCount = req.TupleList.Count,
            ValueTupleWorked = !string.IsNullOrEmpty(req.Person.Name)
        });
    }
}
