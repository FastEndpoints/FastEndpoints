namespace TestCases.RouteBindingTest;

public class Request
{
    /// <summary>
    /// this is a string prop
    /// </summary>
    public string String { get; set; }
    public bool Bool { get; set; }
    public int? Int { get; set; }
    public long Long { get; set; }
    public double Double { get; set; }
    public decimal Decimal { get; set; }
    public int? Blank { get; set; }

    //public IEnumerable<int> Integers { get; set; }

    /// <summary>
    /// this prop will be bound from body
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
