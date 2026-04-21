using NbReader.App.ViewModels;
using NbReader.Catalog;
using NbReader.Import;
using NbReader.Infrastructure;
using NbReader.Search;

namespace NbReader.Import.Tests;

public sealed class MainWindowViewModelWorkflowTests
{
    [Fact]
    public void NavigationItems_ShouldOnlyContainSupportedSections()
    {
        using var fixture = TestRuntimeFixture.Create();
        var viewModel = new MainWindowViewModel(fixture.Runtime);

        Assert.Equal(["Catalog", "Import", "Reader", "Search"], viewModel.NavigationItems);
        Assert.DoesNotContain("Metadata", viewModel.NavigationItems);
        Assert.DoesNotContain("Infrastructure", viewModel.NavigationItems);
    }

    [Fact]
    public void EmptyCatalog_ShouldExposeImportGuidance()
    {
        using var fixture = TestRuntimeFixture.Create();
        var viewModel = new MainWindowViewModel(fixture.Runtime);

        Assert.True(viewModel.IsCatalogEmpty);
        Assert.Contains("Import", viewModel.CatalogPrimaryActionHint, StringComparison.Ordinal);
        Assert.True(viewModel.ShouldShowCatalogPrimaryActionHint);

        viewModel.GoToImportSection();

        Assert.Equal(ImportModule.Name, viewModel.SelectedNavigation);
        Assert.True(viewModel.IsImportSection);
    }

    [Fact]
    public async Task ImportExecution_ShouldRefreshCatalogAndNavigateBackAsync()
    {
        using var fixture = TestRuntimeFixture.Create();
        var importFolder = fixture.CreateSampleImageFolder("SeriesA", "Vol01", imageCount: 3);
        var viewModel = new MainWindowViewModel(fixture.Runtime)
        {
            ImportPathInput = importFolder,
        };

        await viewModel.AnalyzeImportInputAsync();
        await viewModel.ExecuteImportAsync();

        Assert.NotEmpty(viewModel.SeriesCards);
        Assert.Equal(CatalogModule.Name, viewModel.SelectedNavigation);
        Assert.Contains("导入成功", viewModel.ImportExecutionSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnalyzeImport_ShouldRecordRecentImportPathAsync()
    {
        using var fixture = TestRuntimeFixture.Create();
        var first = fixture.CreateSampleImageFolder("SeriesRecent", "Vol01", imageCount: 3);
        var second = fixture.CreateSampleImageFolder("SeriesRecent", "Vol02", imageCount: 3);
        var viewModel = new MainWindowViewModel(fixture.Runtime)
        {
            ImportPathInput = first,
        };

        await viewModel.AnalyzeImportInputAsync();
        viewModel.ImportPathInput = second;
        await viewModel.AnalyzeImportInputAsync();

        Assert.Equal(second, viewModel.RecentImportPaths[0]);
        Assert.Equal(first, viewModel.RecentImportPaths[1]);
        Assert.Equal(second, viewModel.SelectedRecentImportPath);
        Assert.Contains(second, fixture.Runtime.Settings.RecentImportPaths, StringComparer.OrdinalIgnoreCase);
    }

    private sealed class TestRuntimeFixture : IDisposable
    {
        private readonly string _root;

        private TestRuntimeFixture(string root, AppRuntime runtime)
        {
            _root = root;
            Runtime = runtime;
        }

        public AppRuntime Runtime { get; }

        public static TestRuntimeFixture Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "nbreader-vm-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);

            var dbPath = Path.Combine(root, "app.db");
            var settingsPath = Path.Combine(root, "settings.json");
            var logPath = Path.Combine(root, "app.log");

            var database = new AppDatabase(dbPath);
            database.Initialize();

            var logger = new AppLogger(logPath);
            var settingsStore = new AppSettingsStore(settingsPath);
            var settings = new AppSettings();
            settingsStore.Save(settings);

            var runtime = new AppRuntime(
                settings,
                settingsStore,
                database,
                logger,
                new SeriesQueryService(database.ConnectionString),
                new SeriesSearchService(database.ConnectionString),
                new SqliteImportTaskStore(database),
                new ImportOrchestrator(new SqliteImportTaskStore(database)),
                new ImportWriteService(database, logger),
                new LibraryMaintenanceService(database.ConnectionString),
                new SeriesCorrectionService(database.ConnectionString),
                new SeriesMetadataEditService(database.ConnectionString),
                new VolumeQueryService(database.ConnectionString),
                new ReadingProgressService(database.ConnectionString));

            return new TestRuntimeFixture(root, runtime);
        }

        public string CreateSampleImageFolder(string seriesName, string volumeName, int imageCount)
        {
            var seriesDir = Path.Combine(_root, seriesName);
            var volumeDir = Path.Combine(seriesDir, volumeName);
            Directory.CreateDirectory(volumeDir);

            for (var i = 1; i <= imageCount; i++)
            {
                var imagePath = Path.Combine(volumeDir, $"{i:000}.jpg");
                File.WriteAllBytes(imagePath, [0x00]);
            }

            return volumeDir;
        }

        public void Dispose()
        {
            TryDeleteDirectory(_root);
        }

        private static void TryDeleteDirectory(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch
            {
                // Best effort cleanup for temp test artifacts.
            }
        }
    }
}
