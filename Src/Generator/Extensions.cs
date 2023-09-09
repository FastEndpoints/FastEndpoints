#pragma warning disable IDE1006
using System.Text;

namespace FastEndpoints.Generator;
internal static class Extensions
{
    internal static StringBuilder w(this StringBuilder sb, string? val)
    {
        sb.Append(val);
        return sb;
    }
}
