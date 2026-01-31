using FastEndpoints;

namespace NativeAotChecker.Endpoints;

// Positional record for with-expression testing
public record PersonRecord(string FirstName, string LastName, int Age)
{
    public string FullName => $"{FirstName} {LastName}";
}

// Record with init properties
public record AddressRecord
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string ZipCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}

// Request
public class RecordCopyRequest
{
    public PersonRecord? Person { get; set; }
    public AddressRecord? Address { get; set; }
    public string NewLastName { get; set; } = string.Empty;
    public string NewCity { get; set; } = string.Empty;
}

public class RecordCopyResponse
{
    public string OriginalFullName { get; set; } = string.Empty;
    public string ModifiedFullName { get; set; } = string.Empty;
    public string OriginalCity { get; set; } = string.Empty;
    public string ModifiedCity { get; set; } = string.Empty;
    public bool RecordCopyWorked { get; set; }
}

/// <summary>
/// Tests record 'with' expression copying in AOT mode.
/// AOT ISSUE: With expression creates new instance via hidden Clone method.
/// Record copy constructor uses reflection for property mapping.
/// EqualityContract and GetHashCode use runtime type info.
/// </summary>
public class RecordCopyEndpoint : Endpoint<RecordCopyRequest, RecordCopyResponse>
{
    public override void Configure()
    {
        Post("record-copy-test");
        AllowAnonymous();
    }

    public override async Task HandleAsync(RecordCopyRequest req, CancellationToken ct)
    {
        var originalPerson = req.Person ?? new PersonRecord("Default", "Person", 30);
        var originalAddress = req.Address ?? new AddressRecord { City = "DefaultCity" };

        // Use 'with' expression - potentially problematic in AOT
        var modifiedPerson = originalPerson with { LastName = req.NewLastName };
        var modifiedAddress = originalAddress with { City = req.NewCity };

        await Send.OkAsync(new RecordCopyResponse
        {
            OriginalFullName = originalPerson.FullName,
            ModifiedFullName = modifiedPerson.FullName,
            OriginalCity = originalAddress.City,
            ModifiedCity = modifiedAddress.City,
            RecordCopyWorked = modifiedPerson.LastName == req.NewLastName && 
                               modifiedAddress.City == req.NewCity
        });
    }
}
