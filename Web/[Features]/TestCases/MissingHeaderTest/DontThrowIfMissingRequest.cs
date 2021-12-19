namespace TestCases.MissingHeaderTest;

public class DontThrowIfMissingRequest
{
    [FromHeader(IsRequired = false)]
    public string? TenantID { get; set; }
}
