using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace FastEndpoints;

static class FinancialPayloadHash
{
    internal const byte LayoutVersion = 2;

    internal static async ValueTask<byte[]> ComputeAsync(HttpRequest request, CancellationToken ct)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        AppendByte(hasher, LayoutVersion);

        if (request.Query.Count > 0)
            AppendQuery(hasher, request.Query);

        var isForm = request.HasFormContentType;
        if (!request.Body.CanSeek)
            request.EnableBuffering();
        var position = request.Body.Position;

        try
        {
            request.Body.Position = 0;

            if (isForm)
            {
                var form = await request.ReadFormAsync(ct);
                await AppendForm(hasher, form, ct);
            }
            else
            {
                AppendByte(hasher, (byte)'b');
                await AppendStreamAsync(hasher, request.Body, ct);
            }
        }
        finally
        {
            request.Body.Position = position;
        }

        return hasher.GetHashAndReset();
    }

    static void AppendQuery(IncrementalHash hasher, IQueryCollection query)
    {
        AppendByte(hasher, (byte)'q');

        AppendFields(hasher, query.Keys, key => query[key]);
    }

    static async Task AppendForm(IncrementalHash hasher, IFormCollection form, CancellationToken ct)
    {
        AppendByte(hasher, (byte)'f');

        AppendFields(hasher, form.Keys, key => form[key]);

        var files = form.Files.ToArray();

        AppendInt32(hasher, files.Length);

        foreach (var file in files)
        {
            AppendString(hasher, file.Name);
            AppendString(hasher, file.FileName);
            AppendString(hasher, file.ContentType);
            AppendInt64(hasher, file.Length);
            await using var content = file.OpenReadStream();
            await AppendStreamAsync(hasher, content, ct);
        }
    }

    static async Task AppendStreamAsync(IncrementalHash hasher, Stream stream, CancellationToken ct)
    {
        using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = ArrayPool<byte>.Shared.Rent(65536);
        long count = 0;

        try
        {
            int read;

            while ((read = await stream.ReadAsync(buffer, ct)) > 0)
            {
                count = checked(count + read);
                digest.AppendData(buffer.AsSpan(0, read));
            }
            AppendInt64(hasher, count);
            hasher.AppendData(digest.GetHashAndReset());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    static void AppendFields(IncrementalHash hasher, IEnumerable<string> fieldNames, Func<string, StringValues> values)
    {
        var keys = fieldNames.ToArray();
        Array.Sort(keys, StringComparer.Ordinal);
        AppendInt32(hasher, keys.Length);

        foreach (var key in keys)
            AppendNamedValues(hasher, key, values(key));
    }

    static void AppendNamedValues(IncrementalHash hasher, string name, StringValues values)
    {
        AppendString(hasher, name);
        AppendInt32(hasher, values.Count);

        for (var i = 0; i < values.Count; i++)
            AppendString(hasher, values[i] ?? "");
    }

    static void AppendString(IncrementalHash hasher, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        AppendInt32(hasher, bytes.Length);
        hasher.AppendData(bytes);
    }

    static void AppendByte(IncrementalHash hasher, byte value)
        => hasher.AppendData([value]);

    static void AppendInt32(IncrementalHash hasher, int value)
    {
        Span<byte> buf = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(buf, value);
        hasher.AppendData(buf);
    }

    static void AppendInt64(IncrementalHash hasher, long value)
    {
        Span<byte> buf = stackalloc byte[8];
        BinaryPrimitives.WriteInt64LittleEndian(buf, value);
        hasher.AppendData(buf);
    }
}