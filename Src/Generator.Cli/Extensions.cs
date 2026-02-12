using System.Text;
using System.Text.RegularExpressions;

namespace FastEndpoints.Generator.Cli;

static class Extensions
{
    // ReSharper disable once InconsistentNaming
    internal static StringBuilder w(this StringBuilder sb, string? val)
    {
        sb.Append(val);

        return sb;
    }

    // ReSharper disable once InconsistentNaming
    internal static StringBuilder l(this StringBuilder sb, string? val)
    {
        sb.AppendLine(val);

        return sb;
    }

    static readonly Regex _regex = new("[^a-zA-Z0-9]+", RegexOptions.Compiled);

    internal static string Sanitize(this string input, string replacement = "_")
        => _regex.Replace(input, replacement);
}
