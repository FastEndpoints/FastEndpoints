using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

[assembly: InternalsVisibleTo("FastEndpoints")]
[assembly: InternalsVisibleTo("FastEndpoints.Messaging.Remote")]

namespace FastEndpoints;

static class Extensions
{
    internal static string ToHash(this string input)
    {
        using var md5 = MD5.Create();
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = md5.ComputeHash(inputBytes);
        var sb = new StringBuilder();

        for (var i = 0; i < hashBytes.Length; i++)
            sb.Append(hashBytes[i].ToString("x2"));

        return sb.ToString();
    }
}
