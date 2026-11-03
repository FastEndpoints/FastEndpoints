using System.Text;
using System.Text.Json;

namespace FastEndpoints.Agents;

static class InvocationResultHelpers
{
    internal static string ReadBodyText(InvocationResult result)
        => ReadBodyText(result.Body);

    internal static string ReadBodyText(ReadOnlyMemory<byte> body)
        => body.IsEmpty ? string.Empty : Encoding.UTF8.GetString(body.Span);

    internal static bool TryParseJson(InvocationResult result, out JsonElement json)
        => TryParseJson(result.Body, out json);

    internal static bool TryParseJson(ReadOnlyMemory<byte> body, out JsonElement json)
    {
        json = default;

        if (body.IsEmpty)
            return false;

        try
        {
            using var doc = JsonDocument.Parse(body);
            json = doc.RootElement.Clone();

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    internal static string NormalizeMediaType(string? contentType, string defaultMediaType = "application/json")
        => string.IsNullOrWhiteSpace(contentType)
               ? defaultMediaType
               : contentType.Split(';', 2)[0].Trim();
}
