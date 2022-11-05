using System.Linq;
using System.Text.Json.Nodes;

namespace FastEndpoints;
internal static class NumericExtensions
{

    private static readonly Dictionary<Type, Func<string?, JsonNode?>> NumericParsers = new()
    {
        {
            typeof(int),
            value => int.TryParse(value, out var result) ? result : null
        },
        {
            typeof(double),
            value => double.TryParse(value, out var result) ? result : null
        },
        {
            typeof(decimal),
            value => decimal.TryParse(value, out var result) ? result : null
        },
        {
            typeof(long),
            value => long.TryParse(value, out var result) ? result : null
        },
        {
            typeof(short),
            value => short.TryParse(value, out var result) ? result : null
        },
        {
            typeof(sbyte),
            value => sbyte.TryParse(value, out var result) ? result : null
        },
        {
            typeof(byte),
            value => byte.TryParse(value, out var result) ? result : null
        },
        {
            typeof(ulong),
            value => ulong.TryParse(value, out var result) ? result : null
        },
        {
            typeof(ushort),
            value => ushort.TryParse(value, out var result) ? result : null
        },
        {
            typeof(uint),
            value => uint.TryParse(value, out var result) ? result : null
        },
        {
            typeof(float),
            value => float.TryParse(value, out var result) ? result : null
        },
        {
            typeof(bool),
            value => bool.TryParse(value, out var result) ? result : null
        }
    };
    public static Func<string?, JsonNode?>? GetNumericParser(this Type tProp)
    {
        if (tProp.IsEnum)
            return input => Enum.TryParse(tProp, input, true, out var res) ? JsonValue.Create(res) : input;
        return NumericParsers.TryGetValue(tProp, out var parser) ? parser : null;
    }

    public static Func<string?, JsonNode?> GetRequiredNumericParser(this Type myType)
    {
        return NumericParsers[myType];
    }
}
