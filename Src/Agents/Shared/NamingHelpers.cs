using System.Text;

namespace FastEndpoints.Agents;

static class NamingHelpers
{
    /// <summary>
    /// converts a string to snake_case, handling both PascalCase type names and human-readable
    /// title-case strings (spaces and hyphens become underscores).
    /// </summary>
    internal static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var sb = new StringBuilder(input.Length + 8);
        var prevWasSep = false;
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (c is ' ' or '-')
            {
                prevWasSep = true;
                continue;
            }
            if (sb.Length > 0 && (prevWasSep || (char.IsUpper(c) && !char.IsUpper(input[i - 1]))))
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
            prevWasSep = false;
        }
        return sb.ToString();
    }
}
