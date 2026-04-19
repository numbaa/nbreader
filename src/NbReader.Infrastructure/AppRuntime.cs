using NbReader.Catalog;
using NbReader.Search;

namespace NbReader.Infrastructure;

public sealed class AppRuntime
{
    public AppRuntime(
        AppSettings settings,
        AppSettingsStore settingsStore,
        AppDatabase database,
        AppLogger logger,
        SeriesQueryService seriesQueryService,
        SeriesSearchService seriesSearchService,
        LibraryMaintenanceService libraryMaintenanceService,
        SeriesCorrectionService seriesCorrectionService,
        SeriesMetadataEditService seriesMetadataEditService,
        VolumeQueryService volumeQueryService,
        ReadingProgressService readingProgressService)
    {
        Settings = settings;
        SettingsStore = settingsStore;
        Database = database;
        Logger = logger;
        SeriesQueryService = seriesQueryService;
        SeriesSearchService = seriesSearchService;
        LibraryMaintenanceService = libraryMaintenanceService;
        SeriesCorrectionService = seriesCorrectionService;
        SeriesMetadataEditService = seriesMetadataEditService;
        VolumeQueryService = volumeQueryService;
        ReadingProgressService = readingProgressService;
    }

    public AppSettings Settings { get; }

    public AppSettingsStore SettingsStore { get; }

    public AppDatabase Database { get; }

    public AppLogger Logger { get; }

    public SeriesQueryService SeriesQueryService { get; }

    public SeriesSearchService SeriesSearchService { get; }

    public LibraryMaintenanceService LibraryMaintenanceService { get; }

    public SeriesCorrectionService SeriesCorrectionService { get; }

    public SeriesMetadataEditService SeriesMetadataEditService { get; }

    public VolumeQueryService VolumeQueryService { get; }

    public ReadingProgressService ReadingProgressService { get; }
}