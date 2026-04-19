using NbReader.Catalog;
using NbReader.Search;

namespace NbReader.Infrastructure;

public static class AppBootstrapper
{
    public static AppRuntime Initialize()
    {
        AppEnvironment.EnsureCreatedDirectories();

        var logger = new AppLogger(AppEnvironment.CurrentLogFilePath);
        logger.Info("Phase 0 bootstrap started.");

        var settingsStore = new AppSettingsStore(AppEnvironment.SettingsFilePath);
        var settings = settingsStore.LoadOrCreateDefault();
        logger.Info($"Settings loaded from {AppEnvironment.SettingsFilePath}.");

        var database = new AppDatabase(AppEnvironment.DatabaseFilePath);
        database.Initialize();
        logger.Info($"SQLite initialized at {AppEnvironment.DatabaseFilePath}.");

        var seriesQueryService = new SeriesQueryService(database.ConnectionString);
        var seriesSearchService = new SeriesSearchService(database.ConnectionString);
        var volumeQueryService = new VolumeQueryService(database.ConnectionString);
        var readingProgressService = new ReadingProgressService(database.ConnectionString);

        return new AppRuntime(settings, settingsStore, database, logger, seriesQueryService, seriesSearchService, volumeQueryService, readingProgressService);
    }
}