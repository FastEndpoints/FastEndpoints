using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace FastEndpoints;

static class FinancialIdentity
{
    internal const string KeyPrefix = "FE:FIDMP:";

    internal static string Build(HttpRequest request, FinancialIdempotencyOptions opts)
        => Build(request, opts, opts.CallerScope?.Invoke(request.HttpContext) ?? throw new InvalidOperationException("CallerScope is required."));

    internal static string Build(HttpRequest request, FinancialIdempotencyOptions opts, string callerScope)
    {
        if (string.IsNullOrWhiteSpace(callerScope))
            throw new InvalidOperationException("CallerScope is required.");

        var path = request.PathBase.Add(request.Path).Value ?? "";

        switch (path.Length)
        {
            // Routing ignores one terminal separator, not repeated or internal separators.
            case > 1 when path[^1] == '/' && path[^2] != '/':
                path = path[..^1];

                break;
            case 0:
                path = "/";

                break;
        }

        if (!opts.UseCaseSensitivePaths)
            path = path.ToUpperInvariant();

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true);
        writer.Write((byte)2);
        writer.Write(request.Method.ToUpperInvariant());
        writer.Write(request.Scheme.ToUpperInvariant());
        writer.Write((request.Host.Value ?? "").ToUpperInvariant());
        writer.Write(path);
        writer.Write(callerScope);

        foreach (var name in opts.IdentityHeaders)
        {
            writer.Write(name.ToUpperInvariant());
            var values = request.Headers[name];
            writer.Write(values.Count);
            foreach (var value in values)
                writer.Write(value ?? "");
        }
        writer.Flush();

        return KeyPrefix + Convert.ToHexString(SHA256.HashData(stream.GetBuffer().AsSpan(0, (int)stream.Length)));
    }
}