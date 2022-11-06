using System.Text.Json.Serialization;

namespace TestCases.QueryObjectBindingTest;
[JsonSerializable(typeof(Person))]
[JsonSerializable(typeof(Response))]
public sealed partial class ApiSerializerContext : JsonSerializerContext
{
}