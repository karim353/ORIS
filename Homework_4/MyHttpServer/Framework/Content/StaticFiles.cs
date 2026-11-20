namespace MyHttpServer.Framework;

public sealed class StaticFiles(string root)
{
    private static readonly Dictionary<string, string> ContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".webp"] = "image/webp",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".txt"] = "text/plain; charset=utf-8"
    };

    public string? FindFile(string url)
    {
        string current = Path.GetFullPath(root);
        foreach (string part in Uri.UnescapeDataString(url).Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith('.') || part.EndsWith('.') || part.EndsWith(' ') ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.Contains('\\') || part.Contains(':'))
                return null;

            current = Path.Combine(current, part);
            var item = new FileInfo(current);
            if (item.Attributes != (FileAttributes)(-1) && item.Attributes.HasFlag(FileAttributes.ReparsePoint))
                return null;
        }

        if (Directory.Exists(current))
            current = Path.Combine(current, "index.html");
        var file = new FileInfo(current);
        return file.Exists && !file.Attributes.HasFlag(FileAttributes.ReparsePoint) ? current : null;
    }

    public static string ContentType(string file) =>
        ContentTypes.GetValueOrDefault(Path.GetExtension(file), "application/octet-stream");
}
