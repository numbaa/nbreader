using NbReader.Catalog;
using NbReader.Import;
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
        IImportTaskStore importTaskStore,
        ImportOrchestrator importOrchestrator,
        ImportWriteService importWriteService,
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
        ImportTaskStore = importTaskStore;
        ImportOrchestrator = importOrchestrator;
        ImportWriteService = importWriteService;
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

    public IImportTaskStore ImportTaskStore { get; }

    public ImportOrchestrator ImportOrchestrator { get; }

    public ImportWriteService ImportWriteService { get; }

    public LibraryMaintenanceService LibraryMaintenanceService { get; }

    public SeriesCorrectionService SeriesCorrectionService { get; }

    public SeriesMetadataEditService SeriesMetadataEditService { get; }

    public VolumeQueryService VolumeQueryService { get; }

    public ReadingProgressService ReadingProgressService { get; }
}