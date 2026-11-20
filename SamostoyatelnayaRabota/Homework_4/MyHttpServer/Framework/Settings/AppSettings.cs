namespace MyHttpServer.Framework.Settings;

public sealed class AppSettings
{
    public string[] Prefixes { get; set; } = [];
    public string StaticDirectory { get; set; } = "static";
}
