namespace TestCases.JsonArrayBindingToListOfModels;

public class Request
{
    public string Name { get; set; }
}

public class Response : Request
{
    public int Id { get; set; }
}
