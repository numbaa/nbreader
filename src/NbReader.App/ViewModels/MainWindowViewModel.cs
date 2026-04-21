using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using NbReader.Catalog;
using NbReader.Import;
using NbReader.Infrastructure;
using NbReader.Reader;
using NbReader.Search;

namespace NbReader.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private const bool CoverSinglePageInDualMode = true;
    private static readonly TimeSpan ProgressWriteThrottle = TimeSpan.FromSeconds(2);

    private string _currentSection = "Catalog";
    private string _selectedNavigation = "Catalog";
    private string _statusMessage = "正在加载系列列表...";
    private SeriesCardViewModel? _selectedSeries;
    private VolumeCardViewModel? _selectedVolume;
    private string _seriesDetailTitle = "请选择一个系列查看卷列表。";
    private string _readerLaunchSummary = "尚未打开卷。";
    private string _importPathInput = string.Empty;
    private string _importInputKindSummary = "尚未分析输入。";
    private string _importPlanSummary = "请输入路径并点击“分析输入”。";
    private string _importExecutionSummary = "尚未执行导入。";
    private string _importSeriesNameOverride = string.Empty;
    private bool _importSkipDuplicateVolumes = true;
    private bool _importIgnoreWarnings;
    private bool _isImportBusy;
    private bool _importRequiresConfirmation;
    private string _importWarningsSummary = "无";
    private string _importConflictsSummary = "无";
    private ImportTask? _activeImportTask;
    private ImportPlan? _activeImportPlan;
    private bool _isLoadingVolumes;
    private bool _isLoadingSearchWorkspace;
    private bool _isDiagnosticsExpanded;
    private string _searchWorkspaceSummary = "未加载整理视图。";
    private string _seriesRenameInput = string.Empty;
    private string _volumeNumberInput = string.Empty;
    private string _authorNamesInput = string.Empty;
    private string _tagNamesInput = string.Empty;
    private SeriesMergeTargetItemViewModel? _selectedMergeTargetSeries;
    private long _selectedSeriesIdForDetail;
    private Bitmap? _readerPreviewImage;
    private Bitmap? _readerLeftPageImage;
    private Bitmap? _readerRightPageImage;
    private string _continueReadingSummary = "暂无继续阅读记录。";
    private RecentReadingItemViewModel? _selectedRecentReading;
    private RecentReadingItemViewModel? _continueReadingItem;
    private VolumeReaderContext? _activeReaderContext;
    private ReaderState _readerState = ReaderState.Empty;
    private ReaderDisplayMode _readerDisplayMode = ReaderDisplayMode.SinglePage;
    private ReaderReadingDirection _readingDirection = ReaderReadingDirection.LeftToRight;
    private int _maxPageReached;
    private DateTimeOffset _lastProgressWriteAt = DateTimeOffset.MinValue;
    private bool _isProgressWriteInFlight;
    private bool _pendingForcedProgressWrite;
    private FailedImportTaskItemViewModel? _selectedFailedImportTask;
    private readonly Dictionary<int, Bitmap> _pageBitmapCache = [];
    private readonly NearbyPageWindowPolicy _preloadPolicy = new(radius: 1);
    private readonly UnifiedVolumePageSource _pageSource = new();

    public MainWindowViewModel(AppRuntime runtime)
    {
        Runtime = runtime;
        SeriesCards.CollectionChanged += OnSeriesCardsChanged;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppRuntime Runtime { get; }

    public string Title => "NbReader";

    public string Subtitle => "本地漫画库";

    public IReadOnlyList<string> NavigationItems { get; } =
    [
        "Catalog",
        "Import",
        "Reader",
        "Search",
    ];

    public ObservableCollection<SeriesCardViewModel> SeriesCards { get; } = [];

    public ObservableCollection<VolumeCardViewModel> VolumeCards { get; } = [];

    public ObservableCollection<RecentReadingItemViewModel> RecentReadings { get; } = [];

    public ObservableCollection<UnorganizedVolumeItemViewModel> UnorganizedVolumes { get; } = [];

    public ObservableCollection<FailedImportTaskItemViewModel> FailedImportTasks { get; } = [];

    public ObservableCollection<SeriesMergeTargetItemViewModel> MergeTargetSeriesOptions { get; } = [];

    public string SelectedNavigation
    {
        get => _selectedNavigation;
        set
        {
            if (_selectedNavigation == value)
            {
                return;
            }

            _selectedNavigation = value;
            OnPropertyChanged();
            NavigateTo(value);
        }
    }

    public string CurrentSection
    {
        get => _currentSection;
        private set
        {
            if (_currentSection == value)
            {
                return;
            }

            _currentSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCatalogSection));
            OnPropertyChanged(nameof(IsImportSection));
            OnPropertyChanged(nameof(IsSearchSection));
            OnPropertyChanged(nameof(IsPlaceholderSection));
            OnPropertyChanged(nameof(IsReaderSection));
        }
    }

    public bool IsCatalogSection => string.Equals(CurrentSection, CatalogModule.Name, StringComparison.OrdinalIgnoreCase);

    public bool IsImportSection => string.Equals(CurrentSection, ImportModule.Name, StringComparison.OrdinalIgnoreCase);

    public bool IsReaderSection => string.Equals(CurrentSection, "Reader", StringComparison.OrdinalIgnoreCase);

    public bool IsSearchSection => string.Equals(CurrentSection, SearchModule.Name, StringComparison.OrdinalIgnoreCase);

    public bool IsPlaceholderSection => !IsCatalogSection && !IsImportSection && !IsReaderSection && !IsSearchSection;

    public SeriesCardViewModel? SelectedSeries
    {
        get => _selectedSeries;
        set
        {
            if (_selectedSeries == value)
            {
                return;
            }

            _selectedSeries = value;
            OnPropertyChanged();
            if (_selectedSeries is not null)
            {
                StatusMessage = $"已选择系列：{_selectedSeries.Title}（{_selectedSeries.VolumeCount} 卷），正在加载卷列表。";
                SeriesRenameInput = _selectedSeries.Title;
                _ = LoadSelectedSeriesMetadataAsync(_selectedSeries.SeriesId);
                _selectedSeriesIdForDetail = _selectedSeries.SeriesId;
                _ = LoadVolumesForSelectedSeriesAsync(_selectedSeries.SeriesId);
            }
            else
            {
                SeriesDetailTitle = "请选择一个系列查看卷列表。";
                SeriesRenameInput = string.Empty;
                AuthorNamesInput = string.Empty;
                TagNamesInput = string.Empty;
                VolumeCards.Clear();
                SelectedVolume = null;
            }

            OnPropertyChanged(nameof(CanMergeSelectedSeries));
            OnPropertyChanged(nameof(CanApplySeriesRename));
            OnPropertyChanged(nameof(CanApplySeriesMetadata));
        }
    }

    public VolumeCardViewModel? SelectedVolume
    {
        get => _selectedVolume;
        set
        {
            if (_selectedVolume == value)
            {
                return;
            }

            _selectedVolume = value;
            VolumeNumberInput = _selectedVolume?.VolumeNumber?.ToString() ?? string.Empty;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanOpenSelectedVolume));
            OnPropertyChanged(nameof(CatalogPrimaryActionHint));
            OnPropertyChanged(nameof(ShouldShowCatalogPrimaryActionHint));
            OnPropertyChanged(nameof(CanApplyVolumeNumberCorrection));
        }
    }

    public bool CanOpenSelectedVolume => SelectedVolume is not null;

    public bool IsCatalogEmpty => SeriesCards.Count == 0;

    public string CatalogPrimaryActionHint => CanOpenSelectedVolume
        ? ""
        : IsCatalogEmpty
            ? "书架为空：请先到 Import 导入目录或 zip。"
            : "请先在右侧卷列表中选择一个卷。";

    public bool ShouldShowCatalogPrimaryActionHint => !CanOpenSelectedVolume;

    public RecentReadingItemViewModel? SelectedRecentReading
    {
        get => _selectedRecentReading;
        set
        {
            if (_selectedRecentReading == value)
            {
                return;
            }

            _selectedRecentReading = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanOpenSelectedRecentReading));
        }
    }

    public bool CanOpenSelectedRecentReading => SelectedRecentReading is not null;

    public bool CanContinueReading => _continueReadingItem is not null;

    public string ContinueReadingActionHint => CanContinueReading
        ? ""
        : "暂无可继续阅读的记录。";

    public bool ShouldShowContinueReadingActionHint => !CanContinueReading;

    public string ContinueReadingSummary
    {
        get => _continueReadingSummary;
        private set
        {
            if (_continueReadingSummary == value)
            {
                return;
            }

            _continueReadingSummary = value;
            OnPropertyChanged();
        }
    }

    public string SeriesDetailTitle
    {
        get => _seriesDetailTitle;
        private set
        {
            if (_seriesDetailTitle == value)
            {
                return;
            }

            _seriesDetailTitle = value;
            OnPropertyChanged();
        }
    }

    public bool IsLoadingVolumes
    {
        get => _isLoadingVolumes;
        private set
        {
            if (_isLoadingVolumes == value)
            {
                return;
            }

            _isLoadingVolumes = value;
            OnPropertyChanged();
        }
    }

    public string ReaderLaunchSummary
    {
        get => _readerLaunchSummary;
        private set
        {
            if (_readerLaunchSummary == value)
            {
                return;
            }

            _readerLaunchSummary = value;
            OnPropertyChanged();
        }
    }

    public string ImportPathInput
    {
        get => _importPathInput;
        set
        {
            if (_importPathInput == value)
            {
                return;
            }

            _importPathInput = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanAnalyzeImportInput));
        }
    }

    public string ImportInputKindSummary
    {
        get => _importInputKindSummary;
        private set
        {
            if (_importInputKindSummary == value)
            {
                return;
            }

            _importInputKindSummary = value;
            OnPropertyChanged();
        }
    }

    public string ImportPlanSummary
    {
        get => _importPlanSummary;
        private set
        {
            if (_importPlanSummary == value)
            {
                return;
            }

            _importPlanSummary = value;
            OnPropertyChanged();
        }
    }

    public string ImportExecutionSummary
    {
        get => _importExecutionSummary;
        private set
        {
            if (_importExecutionSummary == value)
            {
                return;
            }

            _importExecutionSummary = value;
            OnPropertyChanged();
        }
    }

    public string ImportSeriesNameOverride
    {
        get => _importSeriesNameOverride;
        set
        {
            if (_importSeriesNameOverride == value)
            {
                return;
            }

            _importSeriesNameOverride = value;
            OnPropertyChanged();
        }
    }

    public bool ImportSkipDuplicateVolumes
    {
        get => _importSkipDuplicateVolumes;
        set
        {
            if (_importSkipDuplicateVolumes == value)
            {
                return;
            }

            _importSkipDuplicateVolumes = value;
            OnPropertyChanged();
        }
    }

    public bool ImportIgnoreWarnings
    {
        get => _importIgnoreWarnings;
        set
        {
            if (_importIgnoreWarnings == value)
            {
                return;
            }

            _importIgnoreWarnings = value;
            OnPropertyChanged();
        }
    }

    public bool IsImportBusy
    {
        get => _isImportBusy;
        private set
        {
            if (_isImportBusy == value)
            {
                return;
            }

            _isImportBusy = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanAnalyzeImportInput));
            OnPropertyChanged(nameof(CanExecuteImport));
        }
    }

    public bool ImportRequiresConfirmation
    {
        get => _importRequiresConfirmation;
        private set
        {
            if (_importRequiresConfirmation == value)
            {
                return;
            }

            _importRequiresConfirmation = value;
            OnPropertyChanged();
        }
    }

    public string ImportWarningsSummary
    {
        get => _importWarningsSummary;
        private set
        {
            if (_importWarningsSummary == value)
            {
                return;
            }

            _importWarningsSummary = value;
            OnPropertyChanged();
        }
    }

    public string ImportConflictsSummary
    {
        get => _importConflictsSummary;
        private set
        {
            if (_importConflictsSummary == value)
            {
                return;
            }

            _importConflictsSummary = value;
            OnPropertyChanged();
        }
    }

    public bool CanAnalyzeImportInput => !IsImportBusy && !string.IsNullOrWhiteSpace(ImportPathInput);

    public bool CanExecuteImport => !IsImportBusy && _activeImportTask is not null && _activeImportPlan is not null;

    public void GoToImportSection()
    {
        SelectedNavigation = ImportModule.Name;
    }

    public bool IsLoadingSearchWorkspace
    {
        get => _isLoadingSearchWorkspace;
        private set
        {
            if (_isLoadingSearchWorkspace == value)
            {
                return;
            }

            _isLoadingSearchWorkspace = value;
            OnPropertyChanged();
        }
    }

    public string SearchWorkspaceSummary
    {
        get => _searchWorkspaceSummary;
        private set
        {
            if (_searchWorkspaceSummary == value)
            {
                return;
            }

            _searchWorkspaceSummary = value;
            OnPropertyChanged();
        }
    }

    public FailedImportTaskItemViewModel? SelectedFailedImportTask
    {
        get => _selectedFailedImportTask;
        set
        {
            if (_selectedFailedImportTask == value)
            {
                return;
            }

            _selectedFailedImportTask = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanRetrySelectedFailedImportTask));
        }
    }

    public bool CanRetrySelectedFailedImportTask => SelectedFailedImportTask is not null;

    public string SeriesRenameInput
    {
        get => _seriesRenameInput;
        set
        {
            if (_seriesRenameInput == value)
            {
                return;
            }

            _seriesRenameInput = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanApplySeriesRename));
        }
    }

    public bool CanApplySeriesRename => SelectedSeries is not null && !string.IsNullOrWhiteSpace(SeriesRenameInput);

    public string VolumeNumberInput
    {
        get => _volumeNumberInput;
        set
        {
            if (_volumeNumberInput == value)
            {
                return;
            }

            _volumeNumberInput = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanApplyVolumeNumberCorrection));
        }
    }

    public bool CanApplyVolumeNumberCorrection => SelectedVolume is not null;

    public SeriesMergeTargetItemViewModel? SelectedMergeTargetSeries
    {
        get => _selectedMergeTargetSeries;
        set
        {
            if (_selectedMergeTargetSeries == value)
            {
                return;
            }

            _selectedMergeTargetSeries = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanMergeSelectedSeries));
        }
    }

    public bool CanMergeSelectedSeries => SelectedSeries is not null
        && SelectedMergeTargetSeries is not null
        && SelectedMergeTargetSeries.SeriesId != SelectedSeries.SeriesId;

    public string AuthorNamesInput
    {
        get => _authorNamesInput;
        set
        {
            if (_authorNamesInput == value)
            {
                return;
            }

            _authorNamesInput = value;
            OnPropertyChanged();
        }
    }

    public string TagNamesInput
    {
        get => _tagNamesInput;
        set
        {
            if (_tagNamesInput == value)
            {
                return;
            }

            _tagNamesInput = value;
            OnPropertyChanged();
        }
    }

    public bool CanApplySeriesMetadata => SelectedSeries is not null;

    public Bitmap? ReaderPreviewImage
    {
        get => _readerPreviewImage;
        private set
        {
            if (ReferenceEquals(_readerPreviewImage, value))
            {
                return;
            }

            _readerPreviewImage = value;
            OnPropertyChanged();
        }
    }

    public Bitmap? ReaderLeftPageImage
    {
        get => _readerLeftPageImage;
        private set
        {
            if (ReferenceEquals(_readerLeftPageImage, value))
            {
                return;
            }

            _readerLeftPageImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasReaderLeftPageImage));
        }
    }

    public Bitmap? ReaderRightPageImage
    {
        get => _readerRightPageImage;
        private set
        {
            if (ReferenceEquals(_readerRightPageImage, value))
            {
                return;
            }

            _readerRightPageImage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasReaderRightPageImage));
        }
    }

    public bool HasReaderLeftPageImage => ReaderLeftPageImage is not null;

    public bool HasReaderRightPageImage => ReaderRightPageImage is not null;

    public bool IsSinglePageMode => _readerDisplayMode == ReaderDisplayMode.SinglePage;

    public bool IsDualPageMode => _readerDisplayMode == ReaderDisplayMode.DualPage;

    public string ReaderDisplayModeLabel => IsSinglePageMode ? "单页" : "双页";

    public string ReaderDirectionLabel => _readingDirection == ReaderReadingDirection.LeftToRight ? "从左到右" : "从右到左";

    public string ReaderPageIndicator
    {
        get
        {
            if (_readerState.TotalPages == 0)
            {
                return "0 / 0";
            }

            var spread = ReaderSpreadRules.BuildSpread(
                _readerState.CurrentPageIndex,
                _readerState.TotalPages,
                _readerDisplayMode,
                _readingDirection,
                CoverSinglePageInDualMode);

            if (IsSinglePageMode)
            {
                var current = spread.LeftPageIndex ?? _readerState.CurrentPageIndex;
                return $"{current + 1} / {_readerState.TotalPages}";
            }

            var left = spread.LeftPageIndex is int leftIndex ? (leftIndex + 1).ToString() : "-";
            var right = spread.RightPageIndex is int rightIndex ? (rightIndex + 1).ToString() : "-";
            return $"{left} | {right} / {_readerState.TotalPages}";
        }
    }

    public bool CanGoToPreviousPage => TryGetAdjacentAnchorIndex(isNext: false, out _);

    public bool CanGoToNextPage => TryGetAdjacentAnchorIndex(isNext: true, out _);

    public string ReaderStateLabel => _readerState.Lifecycle switch
    {
        ReaderLifecycle.Idle => "Idle",
        ReaderLifecycle.VolumeReady => "VolumeReady",
        ReaderLifecycle.PageLoading => "PageLoading",
        ReaderLifecycle.PageReady => "PageReady",
        ReaderLifecycle.Error => "Error",
        _ => "Unknown",
    };

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            if (_statusMessage == value)
            {
                return;
            }

            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    public bool IsDiagnosticsExpanded
    {
        get => _isDiagnosticsExpanded;
        set
        {
            if (_isDiagnosticsExpanded == value)
            {
                return;
            }

            _isDiagnosticsExpanded = value;
            OnPropertyChanged();
        }
    }

    public string RuntimeSummary => "系统就绪";

    public string DiagnosticsText =>
        $"SQLite: {Runtime.Database.DatabaseFilePath} | Schema Version: {Runtime.Database.ReadMetaValue("schema_version") ?? "unknown"} | Log: {Runtime.Logger.LogFilePath}";

    public string Footer => DiagnosticsText;

    public void NavigateTo(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return;
        }

        CurrentSection = sectionName;
        if (IsCatalogSection)
        {
            _ = LoadSeriesAsync();
            return;
        }

        if (IsImportSection)
        {
            StatusMessage = "请输入本地 zip 或目录路径后执行导入。";
            return;
        }

        if (IsSearchSection)
        {
            _ = LoadSearchWorkspaceAsync();
            return;
        }

        if (IsPlaceholderSection)
        {
            StatusMessage = $"{CurrentSection} 页面将在后续里程碑实现。";
        }
    }

    public void ReportStatus(string message)
    {
        if (!string.IsNullOrWhiteSpace(message))
        {
            StatusMessage = message;
        }
    }

    public async Task LoadSeriesAsync(CancellationToken cancellationToken = default)
    {
        StatusMessage = "正在加载系列列表...";

        var items = await Runtime.SeriesSearchService.SearchAsync(
            new SeriesSearchQuery(
                SortBy: SeriesSearchSortBy.LatestUpdatedDesc,
                Limit: 200),
            cancellationToken);

        SeriesCards.Clear();
        foreach (var item in items)
        {
            SeriesCards.Add(new SeriesCardViewModel(
                item.SeriesId,
                item.Title,
                item.VolumeCount,
                item.LatestUpdatedAt));
        }

        StatusMessage = items.Count == 0
            ? "尚无系列数据，请先完成导入。"
            : $"已加载 {items.Count} 个系列。";

        if (items.Count > 0)
        {
            SelectedSeries = SeriesCards[0];
        }
        else
        {
            SelectedSeries = null;
        }

        await LoadReadingEntrypointsAsync(cancellationToken);
    }

    public async Task OpenSelectedVolumeAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSeries is null || SelectedVolume is null)
        {
            StatusMessage = "请先选择系列与卷。";
            return;
        }

        await OpenVolumeByIdAsync(SelectedVolume.VolumeId, cancellationToken);
    }

    public async Task OpenContinueReadingAsync(CancellationToken cancellationToken = default)
    {
        if (_continueReadingItem is null)
        {
            StatusMessage = "暂无可继续阅读的记录。";
            return;
        }

        await OpenVolumeByIdAsync(_continueReadingItem.VolumeId, cancellationToken);
    }

    public async Task OpenSelectedRecentReadingAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedRecentReading is null)
        {
            StatusMessage = "请先选择一条最近阅读记录。";
            return;
        }

        await OpenVolumeByIdAsync(SelectedRecentReading.VolumeId, cancellationToken);
    }

    public async Task AnalyzeImportInputAsync(CancellationToken cancellationToken = default)
    {
        if (!CanAnalyzeImportInput)
        {
            StatusMessage = "请先输入有效路径。";
            return;
        }

        IsImportBusy = true;
        try
        {
            var rawInput = ImportPathInput.Trim();
            var task = await Task.Run(() => Runtime.ImportOrchestrator.CreateOrReuseTask(rawInput), cancellationToken);
            var plan = await Task.Run(() => Runtime.ImportOrchestrator.AnalyzeTask(task), cancellationToken);

            _activeImportTask = task;
            _activeImportPlan = plan;

            ImportInputKindSummary = $"输入类型：{task.InputKind}";
            ImportPlanSummary = $"计划卷数：{plan.VolumePlans.Count}，系列候选：{plan.SeriesCandidate ?? "(未识别)"}";
            ImportWarningsSummary = plan.WarningList.Count == 0
                ? "无"
                : string.Join("；", plan.WarningList);
            ImportConflictsSummary = plan.ConflictReport.HasConflicts
                ? string.Join("；", plan.ConflictReport.DetailMessages)
                : "无";
            ImportRequiresConfirmation = plan.RequiresConfirmation;
            ImportSeriesNameOverride = plan.SeriesCandidate ?? string.Empty;
            ImportExecutionSummary = "分析完成，等待执行导入。";
            StatusMessage = plan.RequiresConfirmation
                ? "检测到冲突或告警，执行导入前将应用确认策略。"
                : "分析完成，可直接导入。";
        }
        finally
        {
            IsImportBusy = false;
        }
    }

    public async Task ExecuteImportAsync(CancellationToken cancellationToken = default)
    {
        if (!CanExecuteImport || _activeImportTask is null || _activeImportPlan is null)
        {
            StatusMessage = "请先分析输入后再执行导入。";
            return;
        }

        IsImportBusy = true;
        try
        {
            var task = _activeImportTask;
            var plan = _activeImportPlan;

            var planForPersist = plan;
            if (plan.RequiresConfirmation)
            {
                var request = new ImportConfirmationRequest(
                    SeriesNameOverride: string.IsNullOrWhiteSpace(ImportSeriesNameOverride) ? null : ImportSeriesNameOverride.Trim(),
                    VolumeOverrides: [],
                    SkipDuplicateVolumes: ImportSkipDuplicateVolumes,
                    IgnoreWarnings: ImportIgnoreWarnings);

                planForPersist = Runtime.ImportOrchestrator.ConfirmPlan(task, plan, request);
                _activeImportPlan = planForPersist;
                task = UpdateImportTaskStatus(task, ImportTaskStatus.Importing, "confirmation_applied", "Confirmation applied.");
            }

            var persistResult = await Task.Run(() => Runtime.ImportWriteService.Persist(planForPersist), cancellationToken);
            if (!persistResult.Succeeded || persistResult.Summary is null)
            {
                var error = persistResult.ErrorMessage ?? "未知错误";
                ImportExecutionSummary = $"导入失败：{error}";
                _activeImportTask = UpdateImportTaskStatus(task, ImportTaskStatus.Failed, "import_failed", error);
                StatusMessage = ImportExecutionSummary;
                await LoadSearchWorkspaceAsync(cancellationToken);
                return;
            }

            var summary = persistResult.Summary;
            ImportExecutionSummary = $"导入成功：卷 {summary.InsertedOrUpdatedVolumes}，页面 {summary.InsertedPages}。";
            _activeImportTask = UpdateImportTaskStatus(task, ImportTaskStatus.Completed, "import_completed", ImportExecutionSummary);
            StatusMessage = ImportExecutionSummary;

            await LoadSeriesAsync(cancellationToken);
            await LoadSearchWorkspaceAsync(cancellationToken);
            SelectedNavigation = CatalogModule.Name;
        }
        finally
        {
            IsImportBusy = false;
        }
    }

    public async Task RefreshSearchWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        await LoadSearchWorkspaceAsync(cancellationToken);
    }

    public async Task RetrySelectedFailedImportTaskAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedFailedImportTask is null)
        {
            StatusMessage = "请先选择一条失败记录。";
            return;
        }

        var succeeded = await Runtime.LibraryMaintenanceService.RetryFailedTaskAsync(SelectedFailedImportTask.TaskId, cancellationToken);
        if (!succeeded)
        {
            StatusMessage = "重新处理失败：记录不存在或状态已变化。";
            await LoadSearchWorkspaceAsync(cancellationToken);
            return;
        }

        StatusMessage = $"已提交重新处理：{SelectedFailedImportTask.RawInput}";
        await LoadSearchWorkspaceAsync(cancellationToken);
    }

    public async Task ApplySeriesRenameAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSeries is null)
        {
            StatusMessage = "请先选择一个系列。";
            return;
        }

        var newTitle = SeriesRenameInput.Trim();
        if (string.IsNullOrWhiteSpace(newTitle))
        {
            StatusMessage = "系列名不能为空。";
            return;
        }

        var changed = await Runtime.SeriesCorrectionService.RenameSeriesAsync(SelectedSeries.SeriesId, newTitle, cancellationToken);
        if (!changed)
        {
            StatusMessage = "系列改名失败：记录不存在或名称无效。";
            return;
        }

        await ReloadCatalogSelectionAsync(SelectedSeries.SeriesId, cancellationToken);
        StatusMessage = $"系列已改名为：{newTitle}";
    }

    public async Task MergeSelectedSeriesAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSeries is null || SelectedMergeTargetSeries is null)
        {
            StatusMessage = "请先选择源系列和目标系列。";
            return;
        }

        if (SelectedSeries.SeriesId == SelectedMergeTargetSeries.SeriesId)
        {
            StatusMessage = "源系列与目标系列不能相同。";
            return;
        }

        var sourceId = SelectedSeries.SeriesId;
        var targetId = SelectedMergeTargetSeries.SeriesId;
        var sourceTitle = SelectedSeries.Title;
        var targetTitle = SelectedMergeTargetSeries.Title;

        var merged = await Runtime.SeriesCorrectionService.MergeSeriesAsync(sourceId, targetId, cancellationToken);
        if (!merged)
        {
            StatusMessage = "系列合并失败：请检查系列是否仍存在。";
            return;
        }

        await ReloadCatalogSelectionAsync(targetId, cancellationToken);
        StatusMessage = $"已将“{sourceTitle}”合并到“{targetTitle}”。";
    }

    public async Task ApplySelectedVolumeNumberCorrectionAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedVolume is null)
        {
            StatusMessage = "请先选择一个卷。";
            return;
        }

        int? volumeNumber = null;
        var input = VolumeNumberInput.Trim();
        if (!string.IsNullOrWhiteSpace(input))
        {
            if (!int.TryParse(input, out var parsed) || parsed <= 0)
            {
                StatusMessage = "卷号格式无效，请输入正整数或留空清除。";
                return;
            }

            volumeNumber = parsed;
        }

        var currentSeriesId = SelectedSeries?.SeriesId;
        var volumeId = SelectedVolume.VolumeId;
        var updated = await Runtime.SeriesCorrectionService.UpdateVolumeNumberAsync(volumeId, volumeNumber, cancellationToken);
        if (!updated)
        {
            StatusMessage = "卷号修正失败：卷记录不存在。";
            return;
        }

        if (currentSeriesId is long seriesId)
        {
            await LoadVolumesForSelectedSeriesAsync(seriesId, cancellationToken);
            var updatedVolume = VolumeCards.FirstOrDefault(v => v.VolumeId == volumeId);
            if (updatedVolume is not null)
            {
                SelectedVolume = updatedVolume;
            }
        }

        var shownNumber = volumeNumber?.ToString() ?? "(空)";
        StatusMessage = $"卷号修正完成：{shownNumber}";
    }

    public async Task ApplySeriesMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSeries is null)
        {
            StatusMessage = "请先选择一个系列。";
            return;
        }

        var authors = ParseCsvItems(AuthorNamesInput);
        var tags = ParseCsvItems(TagNamesInput);

        var changed = await Runtime.SeriesMetadataEditService.ReplaceSeriesMetadataAsync(
            SelectedSeries.SeriesId,
            authors,
            tags,
            cancellationToken);

        if (!changed)
        {
            StatusMessage = "作者/标签更新失败：系列不存在。";
            return;
        }

        await LoadSelectedSeriesMetadataAsync(SelectedSeries.SeriesId, cancellationToken);
        await ReloadCatalogSelectionAsync(SelectedSeries.SeriesId, cancellationToken);
        StatusMessage = "作者与标签已更新。";
    }

    public void FlushReadingProgress()
    {
        _ = PersistReadingProgressAsync(force: true);
    }

    private async Task OpenVolumeByIdAsync(long volumeId, CancellationToken cancellationToken = default)
    {
        await PersistReadingProgressAsync(force: true, cancellationToken);

        var context = await Runtime.VolumeQueryService.GetVolumeReaderContextAsync(volumeId, cancellationToken);
        if (context is null)
        {
            StatusMessage = "打开卷失败：未找到卷数据。";
            return;
        }

        if (context.PageLocators.Count == 0)
        {
            StatusMessage = "打开卷失败：卷内没有可用页面。";
            return;
        }

        ClearReaderSession();
        _activeReaderContext = context;
        SetReaderState(ReaderStateMachine.OpenVolume(context.VolumeId, context.VolumeTitle, context.SourcePath, context.PageLocators));

        var savedProgress = await Runtime.ReadingProgressService.GetProgressAsync(context.VolumeId, cancellationToken);
        if (savedProgress is not null)
        {
            _maxPageReached = Math.Max(savedProgress.MaxPageReached, savedProgress.CurrentPageIndex);
            RestoreReaderPreferences(savedProgress);
        }
        else
        {
            _maxPageReached = 0;
        }

        var initialAnchor = savedProgress is null
            ? ReaderSpreadRules.GetInitialAnchorIndex(
                context.PageLocators.Count,
                _readerDisplayMode,
                _readingDirection,
                CoverSinglePageInDualMode)
            : ReaderSpreadRules.NormalizeAnchorIndex(
                savedProgress.CurrentPageIndex,
                context.PageLocators.Count,
                _readerDisplayMode,
                CoverSinglePageInDualMode);

        if (!NavigateToAnchor(initialAnchor))
        {
            StatusMessage = "打开卷失败：无法读取第一页。";
            return;
        }

        NavigateTo("Reader");
        SelectedNavigation = "Reader";

        if (savedProgress is not null)
        {
            StatusMessage = $"已恢复阅读进度：第 {Math.Clamp(savedProgress.CurrentPageIndex + 1, 1, context.PageLocators.Count)} / {context.PageLocators.Count} 页。";
        }

        await LoadReadingEntrypointsAsync(cancellationToken);
    }

    public void ShowPreviousPage()
    {
        if (!TryGetAdjacentAnchorIndex(isNext: false, out var targetAnchor))
        {
            return;
        }

        NavigateToAnchor(targetAnchor);
    }

    public void ShowNextPage()
    {
        if (!TryGetAdjacentAnchorIndex(isNext: true, out var targetAnchor))
        {
            _ = TryOpenNextVolumeAsync();
            return;
        }

        NavigateToAnchor(targetAnchor);
    }

    public void ToggleReaderDisplayMode()
    {
        _readerDisplayMode = IsSinglePageMode ? ReaderDisplayMode.DualPage : ReaderDisplayMode.SinglePage;
        OnPropertyChanged(nameof(IsSinglePageMode));
        OnPropertyChanged(nameof(IsDualPageMode));
        OnPropertyChanged(nameof(ReaderDisplayModeLabel));

        if (_readerState.TotalPages == 0)
        {
            return;
        }

        var targetAnchor = ReaderSpreadRules.NormalizeAnchorIndex(
            _readerState.CurrentPageIndex,
            _readerState.TotalPages,
            _readerDisplayMode,
            CoverSinglePageInDualMode);
        NavigateToAnchor(targetAnchor);
        _ = PersistReadingProgressAsync(force: false);
    }

    public void ToggleReadingDirection()
    {
        _readingDirection = _readingDirection == ReaderReadingDirection.LeftToRight
            ? ReaderReadingDirection.RightToLeft
            : ReaderReadingDirection.LeftToRight;
        OnPropertyChanged(nameof(ReaderDirectionLabel));

        if (_readerState.TotalPages == 0)
        {
            return;
        }

        var mirroredIndex = _readerState.TotalPages - 1 - _readerState.CurrentPageIndex;
        var targetAnchor = ReaderSpreadRules.NormalizeAnchorIndex(
            mirroredIndex,
            _readerState.TotalPages,
            _readerDisplayMode,
            CoverSinglePageInDualMode);
        NavigateToAnchor(targetAnchor);
        _ = PersistReadingProgressAsync(force: false);
    }

    private async Task LoadReadingEntrypointsAsync(CancellationToken cancellationToken = default)
    {
        var recentRows = await Runtime.ReadingProgressService.GetRecentReadingsAsync(limit: 8, cancellationToken);

        RecentReadings.Clear();
        foreach (var row in recentRows)
        {
            RecentReadings.Add(new RecentReadingItemViewModel(row));
        }

        SelectedRecentReading = RecentReadings.Count > 0 ? RecentReadings[0] : null;

        _continueReadingItem = RecentReadings.FirstOrDefault(item => !item.Completed)
            ?? RecentReadings.FirstOrDefault();
        OnPropertyChanged(nameof(CanContinueReading));
        OnPropertyChanged(nameof(ContinueReadingActionHint));
        OnPropertyChanged(nameof(ShouldShowContinueReadingActionHint));

        ContinueReadingSummary = _continueReadingItem is null
            ? "暂无继续阅读记录。"
            : $"继续阅读：{_continueReadingItem.SeriesTitle} / {_continueReadingItem.VolumeTitle}（{_continueReadingItem.CurrentPageText}）";
    }

    private async Task LoadSearchWorkspaceAsync(CancellationToken cancellationToken = default)
    {
        IsLoadingSearchWorkspace = true;
        try
        {
            var unorganizedRows = await Runtime.LibraryMaintenanceService.GetUnorganizedVolumesAsync(cancellationToken: cancellationToken);
            var failedRows = await Runtime.LibraryMaintenanceService.GetFailedImportTasksAsync(cancellationToken: cancellationToken);

            UnorganizedVolumes.Clear();
            foreach (var row in unorganizedRows)
            {
                UnorganizedVolumes.Add(new UnorganizedVolumeItemViewModel(row));
            }

            FailedImportTasks.Clear();
            foreach (var row in failedRows)
            {
                FailedImportTasks.Add(new FailedImportTaskItemViewModel(row));
            }

            await ReloadMergeTargetSeriesOptionsAsync(cancellationToken);

            SelectedFailedImportTask = FailedImportTasks.Count > 0 ? FailedImportTasks[0] : null;
            SearchWorkspaceSummary = $"未整理 {UnorganizedVolumes.Count} 条，导入失败 {FailedImportTasks.Count} 条。";
            StatusMessage = "搜索整理视图已更新。";
        }
        finally
        {
            IsLoadingSearchWorkspace = false;
        }
    }

    private async Task TryOpenNextVolumeAsync(CancellationToken cancellationToken = default)
    {
        if (_activeReaderContext is null)
        {
            return;
        }

        var nextVolumeId = await Runtime.VolumeQueryService.GetNextVolumeIdAsync(_activeReaderContext.VolumeId, cancellationToken);
        if (nextVolumeId is null)
        {
            StatusMessage = "当前已是本系列最后一卷。";
            _ = PersistReadingProgressAsync(force: true, cancellationToken);
            return;
        }

        StatusMessage = "已到卷尾，正在打开下一卷...";
        await OpenVolumeByIdAsync(nextVolumeId.Value, cancellationToken);
    }

    private void RestoreReaderPreferences(ReadingProgressSnapshot progress)
    {
        _readerDisplayMode = ParseDisplayMode(progress.ReadingMode);
        _readingDirection = ParseReadingDirection(progress.ReadingDirection);

        OnPropertyChanged(nameof(IsSinglePageMode));
        OnPropertyChanged(nameof(IsDualPageMode));
        OnPropertyChanged(nameof(ReaderDisplayModeLabel));
        OnPropertyChanged(nameof(ReaderDirectionLabel));
    }

    private async Task LoadVolumesForSelectedSeriesAsync(long seriesId, CancellationToken cancellationToken = default)
    {
        IsLoadingVolumes = true;

        try
        {
            var volumes = await Runtime.VolumeQueryService.GetVolumesBySeriesAsync(seriesId, cancellationToken);

            // Ignore stale results from previous selection switches.
            if (seriesId != _selectedSeriesIdForDetail)
            {
                return;
            }

            VolumeCards.Clear();
            foreach (var volume in volumes)
            {
                VolumeCards.Add(new VolumeCardViewModel(
                    volume.VolumeId,
                    volume.SeriesId,
                    volume.Title,
                    volume.VolumeNumber,
                    volume.PageCount,
                    volume.CreatedAt));
            }

            SelectedVolume = VolumeCards.Count > 0 ? VolumeCards[0] : null;
            var seriesTitle = SelectedSeries?.Title ?? "(未知系列)";
            SeriesDetailTitle = $"{seriesTitle} · 共 {VolumeCards.Count} 卷";

            StatusMessage = VolumeCards.Count == 0
                ? $"系列“{seriesTitle}”暂无卷数据。"
                : $"系列“{seriesTitle}”已加载 {VolumeCards.Count} 卷。";
        }
        finally
        {
            IsLoadingVolumes = false;
        }
    }

    private bool NavigateToAnchor(int targetPageIndex)
    {
        if (_activeReaderContext is null)
        {
            return false;
        }

        var loadingState = ReaderStateMachine.NavigateTo(_readerState, targetPageIndex);
        SetReaderState(loadingState);

        var spread = ReaderSpreadRules.BuildSpread(
            loadingState.CurrentPageIndex,
            loadingState.TotalPages,
            _readerDisplayMode,
            _readingDirection,
            CoverSinglePageInDualMode);

        if (!TryBuildSpreadBitmaps(spread, out var singleBitmap, out var leftBitmap, out var rightBitmap))
        {
            SetReaderState(ReaderStateMachine.MarkError(loadingState, "读取页面失败"));
            StatusMessage = "读取页面失败，请确认资源可访问。";
            return false;
        }

        ReaderPreviewImage = IsSinglePageMode ? singleBitmap : null;
        ReaderLeftPageImage = IsDualPageMode ? leftBitmap : null;
        ReaderRightPageImage = IsDualPageMode ? rightBitmap : null;
        SetReaderState(ReaderStateMachine.MarkPageReady(loadingState));
        UpdateReaderLaunchSummary();
        PreloadAndReleaseAroundCurrentSpread(spread);

        if (spread.VisiblePageIndices.Count > 0)
        {
            _maxPageReached = Math.Max(_maxPageReached, spread.VisiblePageIndices.Max());
        }

        _ = PersistReadingProgressAsync(force: false);

        if (_readerState.TotalPages > 0)
        {
            StatusMessage = $"阅读中：第 {_readerState.CurrentPageNumber} / {_readerState.TotalPages} 页。";
        }

        return true;
    }

    private bool TryBuildSpreadBitmaps(ReaderSpread spread, out Bitmap? singleBitmap, out Bitmap? leftBitmap, out Bitmap? rightBitmap)
    {
        singleBitmap = null;
        leftBitmap = null;
        rightBitmap = null;

        if (IsSinglePageMode)
        {
            if (spread.LeftPageIndex is not int singleIndex)
            {
                return false;
            }

            if (!TryGetOrLoadPageBitmap(singleIndex, out var loadedSingle))
            {
                return false;
            }

            singleBitmap = loadedSingle;
            return true;
        }

        if (spread.LeftPageIndex is int leftIndex)
        {
            if (!TryGetOrLoadPageBitmap(leftIndex, out var loadedLeft))
            {
                return false;
            }

            leftBitmap = loadedLeft;
        }

        if (spread.RightPageIndex is int rightIndex)
        {
            if (!TryGetOrLoadPageBitmap(rightIndex, out var loadedRight))
            {
                return false;
            }

            rightBitmap = loadedRight;
        }

        return leftBitmap is not null || rightBitmap is not null;
    }

    private bool TryGetOrLoadPageBitmap(int pageIndex, out Bitmap bitmap)
    {
        bitmap = null!;

        if (_pageBitmapCache.TryGetValue(pageIndex, out var cached))
        {
            bitmap = cached;
            return true;
        }

        if (_activeReaderContext is null || pageIndex < 0 || pageIndex >= _activeReaderContext.PageLocators.Count)
        {
            return false;
        }

        try
        {
            using var stream = _pageSource.OpenPageStream(_activeReaderContext.SourcePath, _activeReaderContext.PageLocators[pageIndex]);
            if (stream is null)
            {
                return false;
            }

            bitmap = new Bitmap(stream);
            _pageBitmapCache[pageIndex] = bitmap;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void PreloadAndReleaseAroundCurrentSpread(ReaderSpread spread)
    {
        if (_activeReaderContext is null || _readerState.TotalPages == 0)
        {
            return;
        }

        var keepIndices = new HashSet<int>();
        foreach (var visibleIndex in spread.VisiblePageIndices)
        {
            keepIndices.UnionWith(_preloadPolicy.GetWindowIndices(visibleIndex, _readerState.TotalPages));
        }

        foreach (var index in keepIndices)
        {
            if (!_pageBitmapCache.ContainsKey(index))
            {
                TryGetOrLoadPageBitmap(index, out _);
            }
        }

        var staleIndices = _pageBitmapCache.Keys
            .Where(index => !keepIndices.Contains(index))
            .ToArray();

        foreach (var index in staleIndices)
        {
            if (_pageBitmapCache.TryGetValue(index, out var bitmap))
            {
                _pageBitmapCache.Remove(index);
                bitmap.Dispose();
            }
        }
    }

    private void ClearReaderSession()
    {
        ReaderPreviewImage = null;
        ReaderLeftPageImage = null;
        ReaderRightPageImage = null;

        foreach (var bitmap in _pageBitmapCache.Values)
        {
            bitmap.Dispose();
        }

        _pageBitmapCache.Clear();
        _activeReaderContext = null;
        _maxPageReached = 0;
        SetReaderState(ReaderState.Empty);
        ReaderLaunchSummary = "尚未打开卷。";
    }

    private void SetReaderState(ReaderState state)
    {
        _readerState = state;
        OnPropertyChanged(nameof(ReaderStateLabel));
        OnPropertyChanged(nameof(ReaderPageIndicator));
        OnPropertyChanged(nameof(CanGoToPreviousPage));
        OnPropertyChanged(nameof(CanGoToNextPage));
    }

    private bool TryGetAdjacentAnchorIndex(bool isNext, out int targetAnchor)
    {
        targetAnchor = _readerState.CurrentPageIndex;
        if (_readerState.TotalPages == 0)
        {
            return false;
        }

        var rawDirectionStep = _readingDirection == ReaderReadingDirection.LeftToRight ? 1 : -1;
        var delta = isNext ? rawDirectionStep : -rawDirectionStep;
        var candidate = _readerState.CurrentPageIndex + delta;

        while (candidate >= 0 && candidate < _readerState.TotalPages)
        {
            var normalized = ReaderSpreadRules.NormalizeAnchorIndex(
                candidate,
                _readerState.TotalPages,
                _readerDisplayMode,
                CoverSinglePageInDualMode);

            if (normalized != _readerState.CurrentPageIndex)
            {
                targetAnchor = normalized;
                return true;
            }

            candidate += delta;
        }

        return false;
    }

    private async Task PersistReadingProgressAsync(bool force, CancellationToken cancellationToken = default)
    {
        if (_activeReaderContext is null || _readerState.TotalPages == 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (!force && now - _lastProgressWriteAt < ProgressWriteThrottle)
        {
            return;
        }

        if (_isProgressWriteInFlight)
        {
            _pendingForcedProgressWrite = _pendingForcedProgressWrite || force;
            return;
        }

        _isProgressWriteInFlight = true;
        try
        {
            var totalPages = _readerState.TotalPages;
            var normalizedCurrentPage = Math.Clamp(_readerState.CurrentPageIndex, 0, totalPages - 1);
            var normalizedMaxReached = Math.Clamp(Math.Max(_maxPageReached, normalizedCurrentPage), 0, totalPages - 1);
            var completed = normalizedCurrentPage >= totalPages - 1;

            var snapshot = new ReadingProgressSnapshot(
                VolumeId: _activeReaderContext.VolumeId,
                CurrentPageIndex: normalizedCurrentPage,
                MaxPageReached: normalizedMaxReached,
                Completed: completed,
                LastReadAt: now,
                ReadingMode: SerializeDisplayMode(_readerDisplayMode),
                ReadingDirection: SerializeReadingDirection(_readingDirection),
                UpdatedAt: now);

            await Runtime.ReadingProgressService.UpsertProgressAsync(snapshot, cancellationToken);
            _lastProgressWriteAt = now;
        }
        catch (Exception ex)
        {
            Runtime.Logger.Error("Failed to persist reading progress.", ex);
        }
        finally
        {
            _isProgressWriteInFlight = false;
        }

        if (_pendingForcedProgressWrite)
        {
            _pendingForcedProgressWrite = false;
            await PersistReadingProgressAsync(force: true, cancellationToken);
        }
    }

    private static ReaderDisplayMode ParseDisplayMode(string? text)
    {
        return string.Equals(text, "double", StringComparison.OrdinalIgnoreCase)
            ? ReaderDisplayMode.DualPage
            : ReaderDisplayMode.SinglePage;
    }

    private static ReaderReadingDirection ParseReadingDirection(string? text)
    {
        return string.Equals(text, "rtl", StringComparison.OrdinalIgnoreCase)
            ? ReaderReadingDirection.RightToLeft
            : ReaderReadingDirection.LeftToRight;
    }

    private static string SerializeDisplayMode(ReaderDisplayMode mode)
    {
        return mode == ReaderDisplayMode.DualPage ? "double" : "single";
    }

    private static string SerializeReadingDirection(ReaderReadingDirection direction)
    {
        return direction == ReaderReadingDirection.RightToLeft ? "rtl" : "ltr";
    }

    private void UpdateReaderLaunchSummary()
    {
        if (_activeReaderContext is null || _readerState.TotalPages == 0)
        {
            return;
        }

        var seriesTitle = SelectedSeries?.Title ?? "(未知系列)";
        var volumeTitle = SelectedVolume?.Title ?? _activeReaderContext.VolumeTitle;
        ReaderLaunchSummary =
            $"已从系列“{seriesTitle}”打开卷“{volumeTitle}”，共 {_readerState.TotalPages} 页，模式：{ReaderDisplayModeLabel}，方向：{ReaderDirectionLabel}，当前页：{ReaderPageIndicator}。";
    }

    private async Task ReloadCatalogSelectionAsync(long preferredSeriesId, CancellationToken cancellationToken)
    {
        await LoadSeriesAsync(cancellationToken);
        var targetSeries = SeriesCards.FirstOrDefault(card => card.SeriesId == preferredSeriesId)
            ?? SeriesCards.FirstOrDefault();

        if (targetSeries is not null)
        {
            SelectedSeries = targetSeries;
        }

        await ReloadMergeTargetSeriesOptionsAsync(cancellationToken);
    }

    private async Task ReloadMergeTargetSeriesOptionsAsync(CancellationToken cancellationToken)
    {
        var rows = await Runtime.SeriesSearchService.SearchAsync(
            new SeriesSearchQuery(SortBy: SeriesSearchSortBy.TitleAsc, Limit: 500),
            cancellationToken);

        MergeTargetSeriesOptions.Clear();
        foreach (var row in rows)
        {
            MergeTargetSeriesOptions.Add(new SeriesMergeTargetItemViewModel(row.SeriesId, row.Title, row.VolumeCount));
        }

        var selectedSeriesId = SelectedSeries?.SeriesId;
        SelectedMergeTargetSeries = MergeTargetSeriesOptions.FirstOrDefault(option => option.SeriesId != selectedSeriesId)
            ?? MergeTargetSeriesOptions.FirstOrDefault();
        OnPropertyChanged(nameof(CanMergeSelectedSeries));
    }

    private async Task LoadSelectedSeriesMetadataAsync(long seriesId, CancellationToken cancellationToken = default)
    {
        var snapshot = await Runtime.SeriesMetadataEditService.GetSeriesMetadataAsync(seriesId, cancellationToken);
        AuthorNamesInput = string.Join(", ", snapshot.Authors);
        TagNamesInput = string.Join(", ", snapshot.Tags);
    }

    private static IReadOnlyList<string> ParseCsvItems(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return text
            .Split([',', '，', ';', '；', '|', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private ImportTask UpdateImportTaskStatus(ImportTask task, ImportTaskStatus status, string eventType, string message)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = task with
        {
            Status = status,
            UpdatedAt = now,
        };

        Runtime.ImportTaskStore.UpsertTask(updated);
        Runtime.ImportTaskStore.AppendEvent(new ImportTaskEvent(
            updated.TaskId,
            updated.Status,
            eventType,
            message,
            now));
        return updated;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void OnSeriesCardsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(IsCatalogEmpty));
        OnPropertyChanged(nameof(CatalogPrimaryActionHint));
        OnPropertyChanged(nameof(ShouldShowCatalogPrimaryActionHint));
    }
}

public sealed class SeriesCardViewModel
{
    public SeriesCardViewModel(long seriesId, string title, int volumeCount, DateTimeOffset latestUpdatedAt)
    {
        SeriesId = seriesId;
        Title = title;
        VolumeCount = volumeCount;
        LatestUpdatedAt = latestUpdatedAt;
    }

    public long SeriesId { get; }

    public string Title { get; }

    public int VolumeCount { get; }

    public DateTimeOffset LatestUpdatedAt { get; }

    public string LatestUpdatedText => LatestUpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

public sealed class VolumeCardViewModel
{
    public VolumeCardViewModel(long volumeId, long seriesId, string title, int? volumeNumber, int pageCount, DateTimeOffset createdAt)
    {
        VolumeId = volumeId;
        SeriesId = seriesId;
        Title = title;
        VolumeNumber = volumeNumber;
        PageCount = pageCount;
        CreatedAt = createdAt;
    }

    public long VolumeId { get; }

    public long SeriesId { get; }

    public string Title { get; }

    public int? VolumeNumber { get; }

    public int PageCount { get; }

    public DateTimeOffset CreatedAt { get; }

    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd");

    public string VolumeNumberText => VolumeNumber is int number ? number.ToString() : "(未设置)";
}

public sealed class RecentReadingItemViewModel
{
    public RecentReadingItemViewModel(RecentReadingEntry entry)
    {
        VolumeId = entry.VolumeId;
        SeriesId = entry.SeriesId;
        SeriesTitle = entry.SeriesTitle;
        VolumeTitle = entry.VolumeTitle;
        CurrentPageIndex = entry.CurrentPageIndex;
        PageCount = entry.PageCount;
        Completed = entry.Completed;
        LastReadAt = entry.LastReadAt;
    }

    public long VolumeId { get; }

    public long SeriesId { get; }

    public string SeriesTitle { get; }

    public string VolumeTitle { get; }

    public int CurrentPageIndex { get; }

    public int PageCount { get; }

    public bool Completed { get; }

    public DateTimeOffset LastReadAt { get; }

    public string CurrentPageText => PageCount <= 0
        ? "0 / 0"
        : $"{Math.Clamp(CurrentPageIndex + 1, 1, PageCount)} / {PageCount}";

    public string LastReadAtText => LastReadAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

public sealed class UnorganizedVolumeItemViewModel
{
    public UnorganizedVolumeItemViewModel(UnorganizedVolumeEntry entry)
    {
        VolumeId = entry.VolumeId;
        VolumeTitle = entry.VolumeTitle;
        SourcePath = entry.SourcePath;
        CreatedAt = entry.CreatedAt;
    }

    public long VolumeId { get; }

    public string VolumeTitle { get; }

    public string SourcePath { get; }

    public DateTimeOffset CreatedAt { get; }

    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
}

public sealed class FailedImportTaskItemViewModel
{
    public FailedImportTaskItemViewModel(FailedImportTaskEntry entry)
    {
        TaskId = entry.TaskId;
        RawInput = entry.RawInput;
        NormalizedLocator = entry.NormalizedLocator;
        InputKind = entry.InputKind;
        Status = entry.Status;
        UpdatedAt = entry.UpdatedAt;
        LastErrorMessage = entry.LastErrorMessage;
    }

    public Guid TaskId { get; }

    public string RawInput { get; }

    public string NormalizedLocator { get; }

    public string InputKind { get; }

    public string Status { get; }

    public DateTimeOffset UpdatedAt { get; }

    public string? LastErrorMessage { get; }

    public string UpdatedAtText => UpdatedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");

    public string ErrorText => string.IsNullOrWhiteSpace(LastErrorMessage)
        ? "(无错误详情)"
        : LastErrorMessage;
}

public sealed class SeriesMergeTargetItemViewModel
{
    public SeriesMergeTargetItemViewModel(long seriesId, string title, int volumeCount)
    {
        SeriesId = seriesId;
        Title = title;
        VolumeCount = volumeCount;
    }

    public long SeriesId { get; }

    public string Title { get; }

    public int VolumeCount { get; }

    public string DisplayText => $"{Title}（{VolumeCount} 卷）";
}