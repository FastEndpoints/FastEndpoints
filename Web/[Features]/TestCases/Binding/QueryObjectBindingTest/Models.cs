namespace TestCases.QueryObjectBindingTest;

public class Person
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public NestedPerson Child { get; set; }
    public List<int> Numbers { get; set; }
    public DayOfWeek FavoriteDay { get; set; }
    public ByteEnum ByteEnum { get; set; }
    public bool IsHidden { get; set; }
}

public class NestedPerson
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Strings { get; set; }
    public List<DayOfWeek> FavoriteDays { get; set; }
    public bool IsHidden { get; set; }
}

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
    public DayOfWeek Enum { get; set; }

    [FromQueryParams]
    public Person Person { get; set; }
}

public class Response
{
    public string String { get; set; }
    public bool Bool { get; set; }
    public int? Int { get; set; }
    public long Long { get; set; }
    public double Double { get; set; }
    public DayOfWeek Enum { get; set; }
    public Person Person { get; set; }
}

public enum ByteEnum : byte
{
    Check,
    Test,
    AnotherCheck
}