using System.Security.Cryptography;

namespace FastEndpoints.Generator.Cli;

static class FileSetHash
{
    internal static string Compute(IEnumerable<string> files, IEnumerable<string>? additionalFiles = null)
    {
        using var sha256 = SHA256.Create();
        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream);

        foreach (var file in files.OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            writer.WriteLine(file);
            writer.WriteLine(File.GetLastWriteTimeUtc(file).Ticks);
        }

        if (additionalFiles != null)
        {
            foreach (var file in additionalFiles.Where(File.Exists).OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
            {
                writer.WriteLine(file);
                writer.WriteLine(File.GetLastWriteTimeUtc(file).Ticks);
            }
        }

        writer.Flush();
        stream.Position = 0;

        return Convert.ToHexString(sha256.ComputeHash(stream));
    }
}
