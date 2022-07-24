namespace TestCases.DupeParamBindingForIEnumerableProps;

public class Request
{
    public string[] Strings { get; set; }
    public string[] MoreStrings { get; set; }
    public double[] Doubles { get; set; }
    public IEnumerable<int> Ints { get; set; }
    public List<Guid> Guids { get; set; }
    public ICollection<DateTime> Dates { get; set; }
}

public class Response : Request
{
}
