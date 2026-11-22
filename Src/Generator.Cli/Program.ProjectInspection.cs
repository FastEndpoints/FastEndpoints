using System.Xml.Linq;

namespace FastEndpoints.Generator.Cli;

partial class Program
{
    private static SourceFileSet CollectProjectSourceFilesWithHash(string projectPath)
    {
        var projectDir = Path.GetDirectoryName(projectPath)!;
        var files = EnumerateProjectSourceFiles(projectDir);

        return new(files, FileSetHash.Compute(files, [projectPath]));
    }

    private static SourceFileSet CollectReferencedProjectSourceFilesWithHash(string projectPath)
    {
        var referencedProjectPaths = GetReferencedProjectPaths(projectPath);
        var files = new List<string>();

        foreach (var referencedProjectPath in referencedProjectPaths)
        {
            var referencedProjectDir = Path.GetDirectoryName(referencedProjectPath);

            if (referencedProjectDir == null)
                continue;

            files.AddRange(EnumerateProjectSourceFiles(referencedProjectDir));
        }

        return new(files, FileSetHash.Compute(files, referencedProjectPaths));
    }

    private static List<string> EnumerateProjectSourceFiles(string projectDir)
    {
        var separator = Path.DirectorySeparatorChar;
        var binSep = $"{separator}bin{separator}";
        var objSep = $"{separator}obj{separator}";

        var files = new List<string>();

        foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
        {
            if (!file.Contains(binSep) && !file.Contains(objSep) && !file.EndsWith(".g.cs", StringComparison.OrdinalIgnoreCase))
                files.Add(file);
        }

        return files;
    }

    private static string GetGeneratorOutputPath(string projectDir, string? customOutputPath)
    {
        if (!string.IsNullOrWhiteSpace(customOutputPath))
        {
            return Path.IsPathRooted(customOutputPath)
                       ? customOutputPath
                       : Path.Combine(projectDir, customOutputPath);
        }

        return Path.Combine(projectDir, "Generated", "FastEndpoints");
    }

    private static List<string> GetReferencedProjectPaths(string projectPath)
    {
        var visitedProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var referencedProjects = new List<string>();

        CollectReferencedProjectPaths(projectPath, visitedProjects, referencedProjects);

        return referencedProjects;
    }

    private static void CollectReferencedProjectPaths(string projectPath, HashSet<string> visitedProjects, List<string> referencedProjects)
    {
        projectPath = Path.GetFullPath(projectPath);

        if (!visitedProjects.Add(projectPath) || !File.Exists(projectPath))
            return;

        XDocument doc;

        try
        {
            doc = XDocument.Load(projectPath);
        }
        catch
        {
            return;
        }

        var projectDir = Path.GetDirectoryName(projectPath)!;
        var projectReferences = doc.Descendants()
                                   .Where(e => e.Name.LocalName == "ProjectReference")
                                   .Select(e => e.Attribute("Include")?.Value)
                                   .Where(v => !string.IsNullOrWhiteSpace(v));

        foreach (var projectReference in projectReferences)
        {
            var normalizedReference = projectReference!.NormalizeSeparators();
            var referencedProjectPath = Path.GetFullPath(Path.Combine(projectDir, normalizedReference));

            if (!File.Exists(referencedProjectPath) || visitedProjects.Contains(referencedProjectPath))
                continue;

            referencedProjects.Add(referencedProjectPath);
            CollectReferencedProjectPaths(referencedProjectPath, visitedProjects, referencedProjects);
        }
    }
}