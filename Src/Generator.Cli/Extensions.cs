using System.Text;
using System.Text.RegularExpressions;

namespace FastEndpoints.Generator.Cli;

static partial class Extensions
{
    // ReSharper disable  InconsistentNaming
    extension(StringBuilder sb)
    {
        internal StringBuilder w(string? val)
        {
            sb.Append(val);

            return sb;
        }

        internal StringBuilder l(string? val)
        {
            sb.AppendLine(val);

            return sb;
        }
    }

    [GeneratedRegex("[^a-zA-Z0-9]+", RegexOptions.Compiled)]
    private static partial Regex SanitizationRegex();

    internal static string Sanitize(this string input, string replacement = "_")
        => SanitizationRegex().Replace(input, replacement);
}