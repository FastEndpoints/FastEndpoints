using System.Security.Cryptography;
using System.Text;

namespace FastEndpoints;

//the source generator and the library both compile this file (the library by link), so compile-time 'Allow' codes and the codes
//'AccessControl(...)' applies at runtime come from the same algorithm. changing the output changes codes that users have already persisted.
static class AclHash
{
    //the code for a permission name, whether it comes from an 'AccessControl(...)' call or a hand-written partial 'Allow' property.
    //each run of characters other than ascii letters and digits is replaced with a single '_' first, like the generated 'Allow' member name.
    internal static string Compute(string permissionName)
    {
        using var sha256 = SHA256.Create();
        var base64Hash = Convert.ToBase64String(sha256.ComputeHash(Encoding.UTF8.GetBytes(Sanitize(permissionName).ToUpperInvariant())));

        var code = new char[3];
        var count = 0;

        for (var i = 0; i < base64Hash.Length && count < code.Length; i++)
        {
            if (char.IsLetterOrDigit(base64Hash[i]))
                code[count++] = char.ToUpperInvariant(base64Hash[i]);
        }

        return new(code, 0, count);
    }

    //same result as Regex.Replace(input, "[^a-zA-Z0-9]+", "_"). NamespaceExtensions.ToValidIdentifier() applies the same rule but can't be linked
    //into the library (it depends on roslyn), and a hand-written loop keeps the regex engine out of native aot apps.
    static string Sanitize(string input)
    {
        var sb = new StringBuilder(input.Length);
        var inRun = false;

        foreach (var c in input)
        {
            if (c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9')
            {
                sb.Append(c);
                inRun = false;
            }
            else if (!inRun)
            {
                sb.Append('_');
                inRun = true;
            }
        }

        return sb.ToString();
    }
}
