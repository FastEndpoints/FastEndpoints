namespace TestCases.JsonArrayBindingToIEnumerableDto;

public sealed class Request : List<Item>
{
}

public class Item
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class Response : Item
{
}
