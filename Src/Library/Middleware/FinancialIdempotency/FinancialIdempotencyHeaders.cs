using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace FastEndpoints;

static class FinancialIdempotencyHeaders
{
    static readonly HashSet<string> _noisy = new(StringComparer.OrdinalIgnoreCase)
    {
        HeaderNames.Accept,
        HeaderNames.AcceptEncoding,
        HeaderNames.CacheControl,
        HeaderNames.Connection,
        HeaderNames.ContentLength,
        HeaderNames.ContentType,
        HeaderNames.Host,
        HeaderNames.KeepAlive,
        HeaderNames.UserAgent
    };

    internal static bool IsNoisy(string name)
        => _noisy.Contains(name);

    internal static bool IsHopByHop(string name)
        => name.Equals("Keep-Alive", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("Proxy-Authenticate", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("Proxy-Authorization", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("TE", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("Trailer", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) ||
           name.Equals(HeaderNames.TransferEncoding, StringComparison.OrdinalIgnoreCase) ||
           name.Equals(HeaderNames.Connection, StringComparison.OrdinalIgnoreCase);

    internal static List<KeyValuePair<string, string[]>> SnapshotHeaders(IHeaderDictionary headers)
    {
        var list = new List<KeyValuePair<string, string[]>>(headers.Count);
        var nominated = headers.Connection.ToString().Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var header in headers)
        {
            if (nominated.Contains(header.Key, StringComparer.OrdinalIgnoreCase) ||
                header.Key.Equals(HeaderNames.SetCookie, StringComparison.OrdinalIgnoreCase) ||
                IsHopByHop(header.Key) ||
                header.Key.Equals(HeaderNames.ContentLength, StringComparison.OrdinalIgnoreCase))
                continue;

            list.Add(new(header.Key, header.Value.Select(v => v ?? "").ToArray()));
        }

        return list;
    }

    internal static List<string> IdentityHeaderNames(string headerName, IEnumerable<string> additionalHeaders)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { headerName };

        foreach (var header in additionalHeaders)
        {
            if (IsNoisy(header) || header.Equals(headerName, StringComparison.OrdinalIgnoreCase))
                continue;

            names.Add(header);
        }

        var list = names.ToList();
        list.Sort(StringComparer.OrdinalIgnoreCase);

        return list;
    }
}
