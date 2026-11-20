using System.Text.Json;

namespace MyHttpServer.Framework;

public sealed record SettingsManager(string[] Prefixes, string StaticRoot)
{
    private static readonly Lazy<SettingsManager> Current = new(Load);

    public static SettingsManager Instance => Current.Value;

    private static SettingsManager Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "settings.json");
        var settings = JsonSerializer.Deserialize<SettingsManager>(File.ReadAllText(path),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (settings?.Prefixes is not { Length: > 0 } ||
            settings.Prefixes.Any(prefix => !Uri.TryCreate(prefix, UriKind.Absolute, out var address)
                || address.Scheme != "http" || !prefix.EndsWith('/')))
            throw new InvalidDataException("Укажи HTTP-адреса с / на конце в settings.json.");
        return settings with { StaticRoot = Path.GetFullPath(settings.StaticRoot, AppContext.BaseDirectory) };
    }
}
