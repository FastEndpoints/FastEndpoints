namespace TestCases.RateLimitTests;

public class Response
{
    [BindFrom("customerId")]
    public int CustomerID { get; set; }

    /// <summary>
    /// optional other id
    /// </summary>
    [BindFrom("otherID")]
    public int? OtherID { get; set; }
}