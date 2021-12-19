namespace TestCases.MissingHeaderTest;

public class ThrowIfMissingRequest
{
    [FromHeader]
    public string? TenantID { get; set; }
}
