using System.Text;
using System.Text.Json;

namespace FastEndpoints.Agents;

static class InvocationResultHelpers
{
    internal static string ReadBodyText(InvocationResult result)
        => ReadBodyText(result.Body);

    internal static string ReadBodyText(byte[] body)
        => body.Length == 0 ? string.Empty : Encoding.UTF8.GetString(body);

    internal static bool TryParseJson(string text, out JsonElement json)
    {
        json = default;

        if (string.IsNullOrWhiteSpace(text))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(text);
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
