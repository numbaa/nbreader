using System.IO;
using System.Text.Json;

namespace NbReader.Infrastructure;

public sealed class AppSettingsStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public string SettingsFilePath { get; }

    public AppSettingsStore(string settingsFilePath)
    {
        SettingsFilePath = settingsFilePath;
    }

    public AppSettings LoadOrCreateDefault()
    {
        if (!File.Exists(SettingsFilePath))
        {
            var defaultSettings = new AppSettings();
            Save(defaultSettings);
            return defaultSettings;
        }

        var json = File.ReadAllText(SettingsFilePath);
        var settings = JsonSerializer.Deserialize<AppSettings>(json, SerializerOptions);
        return settings ?? new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(SettingsFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        File.WriteAllText(SettingsFilePath, json);
    }
}