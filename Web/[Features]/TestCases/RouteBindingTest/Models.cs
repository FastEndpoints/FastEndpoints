namespace TestCases.RouteBindingTest;

public class Request
{
    public string String { get; set; }
    public bool Bool { get; set; }
    public int? Int { get; set; }
    public long Long { get; set; }
    public double Double { get; set; }
    public decimal Decimal { get; set; }
    public int? Blank { get; set; }

    //public IEnumerable<int> Integers { get; set; }

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
