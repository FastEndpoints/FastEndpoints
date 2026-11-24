using System.Text;

namespace FastEndpoints.Generator.Cli;

static class Extensions
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

    internal static string NormalizeSeparators(this string path)
        => path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
}