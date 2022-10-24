namespace TestCases.MapperTest;

public class Person
{
    public string Name { get; set; }
    public int Age { get; set; }
}

public class Request
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public int Age { get; set; }
}

public class Response
{
    public string Name { get; set; }
    public int Age { get; set; }
}
