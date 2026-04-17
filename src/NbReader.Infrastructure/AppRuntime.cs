using NbReader.Catalog;

namespace NbReader.Infrastructure;

public sealed class AppRuntime
{
    public AppRuntime(
        AppSettings settings,
        AppSettingsStore settingsStore,
        AppDatabase database,
        AppLogger logger,
        SeriesQueryService seriesQueryService,
        VolumeQueryService volumeQueryService)
    {
        Settings = settings;
        SettingsStore = settingsStore;
        Database = database;
        Logger = logger;
        SeriesQueryService = seriesQueryService;
        VolumeQueryService = volumeQueryService;
    }

    public AppSettings Settings { get; }

    public AppSettingsStore SettingsStore { get; }

    public AppDatabase Database { get; }

    public AppLogger Logger { get; }

    public SeriesQueryService SeriesQueryService { get; }

    public VolumeQueryService VolumeQueryService { get; }
}