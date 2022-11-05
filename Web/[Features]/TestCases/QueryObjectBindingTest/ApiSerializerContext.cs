using System.Text.Json.Serialization;

namespace TestCases.QueryObjectBindingTest;

[JsonSerializable(typeof(Request))]
[JsonSerializable(typeof(Response))]
public sealed partial class ApiSerializerContext : JsonSerializerContext
{
}