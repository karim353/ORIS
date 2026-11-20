namespace MyHttpServer.Framework.Utils;

public static class MimeTypes
{
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".html"] = "text/html; charset=utf-8",
        [".css"] = "text/css; charset=utf-8",
        [".js"] = "text/javascript; charset=utf-8",
        [".json"] = "application/json; charset=utf-8",
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon"
    };

    public static string Get(string filePath)
    {
        string extension = Path.GetExtension(filePath);
        return Types.TryGetValue(extension, out string? type)
            ? type
            : "application/octet-stream";
    }
}
