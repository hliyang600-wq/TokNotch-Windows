namespace TokNotch.UI;

/// <summary>Portable user data is relative to the executable, never to the build tree.</summary>
internal static class ApplicationPaths
{
    internal static string DataRoot => Path.Combine(AppContext.BaseDirectory, "data");

    // Developer-only reports prefer the source checkout. Installed diagnostics stay portable.
    internal static string ReportRoot
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
                if (File.Exists(Path.Combine(directory.FullName, "TokNotchWindows.sln")))
                    return directory.FullName;
            return DataRoot;
        }
    }

    internal static string ArtifactsDirectory
    {
        get
        {
            var path = Path.Combine(ReportRoot, "artifacts");
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
