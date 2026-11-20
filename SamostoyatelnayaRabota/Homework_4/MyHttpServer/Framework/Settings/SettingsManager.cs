using System.Text.Json;

namespace MyHttpServer.Framework.Settings;

public sealed class SettingsManager
{
    private static readonly Lazy<SettingsManager> LazyInstance = new(() => new SettingsManager());

    public static SettingsManager Instance => LazyInstance.Value;
    public AppSettings Settings { get; }

    private SettingsManager()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "settings.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("Не найден settings.json рядом с exe.", path);

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        Settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path), options)
                   ?? throw new InvalidOperationException("settings.json пустой");

        if (Settings.Prefixes.Length == 0)
            throw new InvalidOperationException("В settings.json нет адресов в Prefixes");
    }
}
