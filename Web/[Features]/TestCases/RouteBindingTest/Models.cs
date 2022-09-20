using FluentValidation;
using System.Text.Json.Serialization;

namespace TestCases.RouteBindingTest;

public class Custom //: IParseable<Custom>
{
    public int Value { get; set; }

    public static bool TryParse(string? input, out Custom? output)
    {
        if (input == null)
        {
            output = null;
            return false;
        }
        output = new() { Value = int.Parse(input) };
        return true;
    }
}

public class CustomList : List<int>
{
    public static bool TryParse(string? input, out CustomList? output)
    {
        if (string.IsNullOrEmpty(input))
        {
            output = null;
            return false;
        }
        output = new CustomList();
        foreach (var item in input.Split(';'))
        {
            if (int.TryParse(item, out var id))
            {
                output.Add(id);
            }
            else
            {
                output = null;
                return false;
            }
        }
        return true;
    }
}

public class Person
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public NestedPerson Child { get; set; }
    public List<int> Numbers { get; set; }
}

public class NestedPerson
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public List<string> Strings { get; set; }
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
    public Uri? Url { get; set; }
    public Custom Custom { get; set; }
    public CustomList CustomList { get; set; }

    [BindFrom("decimal")]
    public decimal DecimalNumber { get; set; }

    [BindFrom("XBlank")]
    public int? Blank { get; set; }

    [FromQueryParams]
    public Person Person { get; set; }

    /// <summary>
    /// frm body xml comment
    /// </summary>
    public string FromBody { get; set; }

    [JsonIgnore]
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
    public string? Url { get; set; }
    public Custom Custom { get; set; }
    public CustomList CustomList { get; set; }
    public Person Person { get; set; }
}
