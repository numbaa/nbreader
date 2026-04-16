namespace NbReader.Infrastructure;

public sealed class AppRuntime
{
    public AppRuntime(
        AppSettings settings,
        AppSettingsStore settingsStore,
        AppDatabase database,
        AppLogger logger)
    {
        Settings = settings;
        SettingsStore = settingsStore;
        Database = database;
        Logger = logger;
    }

    public AppSettings Settings { get; }

    public AppSettingsStore SettingsStore { get; }

    public AppDatabase Database { get; }

    public AppLogger Logger { get; }
}