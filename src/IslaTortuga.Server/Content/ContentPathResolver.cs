namespace IslaTortuga.Server.Content;

public static class ContentPathResolver
{
    public static string ResolveContentRoot(string contentRootPath)
    {
        var configuredRoot = Environment.GetEnvironmentVariable("CONTENT_PACKS_ROOT");
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        var current = new DirectoryInfo(contentRootPath);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, "content-packs");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return Path.Combine(contentRootPath, "content-packs");
    }
}
