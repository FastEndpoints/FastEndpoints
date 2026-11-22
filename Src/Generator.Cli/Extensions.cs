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

    [GeneratedRegex("[^a-zA-Z0-9]+")]
    private static partial Regex SanitizationRegex();

    internal static string Sanitize(this string input, string replacement = "_")
        => SanitizationRegex().Replace(input, replacement);

    [GeneratedRegex("[^a-zA-Z0-9.]+")]
    private static partial Regex NamespaceRegex();

    //must match ToValidNameSpace() in Src/Generator/Extensions.cs so the namespaces of the source generators and the cli agree
    internal static string ToValidNameSpace(this string input)
        => NamespaceRegex().Replace(input, "_");

    internal static string NormalizeSeparators(this string path)
        => path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
}