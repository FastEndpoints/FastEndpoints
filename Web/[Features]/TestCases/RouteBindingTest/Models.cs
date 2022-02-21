namespace TestCases.RouteBindingTest;

public class Request
{
    /// <summary>
    /// this is a string prop xml comment
    /// </summary>
    public string String { get; set; }
    public bool Bool { get; set; }
    public int? Int { get; set; }
    public long Long { get; set; }
    public double Double { get; set; }

    [BindFrom("Decimal"), Newtonsoft.Json.JsonProperty("Decimal")]
    public decimal DecimalNumber { get; set; }

    [BindFrom("XBlank")]
    public int? Blank { get; set; }

    /// <summary>
    /// frm body xml comment
    /// </summary>
    public string FromBody { get; set; }

    public int ReadOnly => 100;
}

public class Validator : Validator<Request>
{
    public Validator()
    {
        RuleFor(x => x.FromBody).Must(x => x != "xxx");
    }
}

public class Response
{
    public string String { get; set; }
    public bool Bool { get; set; }
    public int Int { get; set; }
    public long Long { get; set; }
    public double Double { get; set; }
    public decimal Decimal { get; set; }
    public int? Blank { get; set; }
    public string FromBody { get; set; }
}
