namespace TestCases.QueryObjectWithObjectsArrayBindingTest;

public class Person
{
    public NestedPerson Child { get; set; }
    public List<ObjectInArray> Objects { get; set; }
    public List<ObjectInArray[]> ArraysOfObjects { get; set; }
}

public class NestedPerson
{
    public List<ObjectInArray> Objects { get; set; }
}

public class Request
{

    [FromQueryParams]
    public Person Person { get; set; }
}

public class ObjectInArray
{
    public string String { get; set; }
    public bool Bool { get; set; }
    public int? Int { get; set; }
    public long Long { get; set; }
    public double Double { get; set; }
    public DayOfWeek Enum { get; set; }
}

public class Response
{
    public Person Person { get; set; }
}

public enum ByteEnum : byte
{
    Check,
    Test,
    AnotherCheck
}