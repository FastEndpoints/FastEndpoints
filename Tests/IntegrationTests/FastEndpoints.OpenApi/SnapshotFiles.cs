namespace OpenApi;

static class SnapshotFiles
{
    internal static string ForFramework(string fileName)
#if NET11_0_OR_GREATER
        => Path.Combine("Snapshots", "net11.0", fileName);
#else
        => fileName;
#endif
}
