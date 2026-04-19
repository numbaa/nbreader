using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using NbReader.Catalog;
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
    private bool _isLoadingVolumes;
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
    private readonly Dictionary<int, Bitmap> _pageBitmapCache = [];
    private readonly NearbyPageWindowPolicy _preloadPolicy = new(radius: 1);
    private readonly UnifiedVolumePageSource _pageSource = new();

    public MainWindowViewModel(AppRuntime runtime)
    {
        Runtime = runtime;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppRuntime Runtime { get; }

    public string Title => "NbReader";

    public string Subtitle => "搜索与整理闭环 - M2（FTS 与拼音索引）";

    public IReadOnlyList<string> NavigationItems { get; } =
    [
        "Catalog",
        "Import",
        "Reader",
        "Search",
        "Metadata",
        "Infrastructure",
    ];

    public ObservableCollection<SeriesCardViewModel> SeriesCards { get; } = [];

    public ObservableCollection<VolumeCardViewModel> VolumeCards { get; } = [];

    public ObservableCollection<RecentReadingItemViewModel> RecentReadings { get; } = [];

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
            OnPropertyChanged(nameof(IsPlaceholderSection));
            OnPropertyChanged(nameof(IsReaderSection));
        }
    }

    public bool IsCatalogSection => string.Equals(CurrentSection, CatalogModule.Name, StringComparison.OrdinalIgnoreCase);

    public bool IsReaderSection => string.Equals(CurrentSection, "Reader", StringComparison.OrdinalIgnoreCase);

    public bool IsPlaceholderSection => !IsCatalogSection && !IsReaderSection;

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
                _selectedSeriesIdForDetail = _selectedSeries.SeriesId;
                _ = LoadVolumesForSelectedSeriesAsync(_selectedSeries.SeriesId);
            }
            else
            {
                SeriesDetailTitle = "请选择一个系列查看卷列表。";
                VolumeCards.Clear();
                SelectedVolume = null;
            }
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
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanOpenSelectedVolume));
        }
    }

    public bool CanOpenSelectedVolume => SelectedVolume is not null;

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

    public string Footer =>
        $"SQLite: {Runtime.Database.DatabaseFilePath} | Schema Version: {Runtime.Database.ReadMetaValue("schema_version") ?? "unknown"} | Log: {Runtime.Logger.LogFilePath}";

    public void NavigateTo(string sectionName)
    {
        if (string.IsNullOrWhiteSpace(sectionName))
        {
            return;
        }

        CurrentSection = sectionName;
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

        ContinueReadingSummary = _continueReadingItem is null
            ? "暂无继续阅读记录。"
            : $"继续阅读：{_continueReadingItem.SeriesTitle} / {_continueReadingItem.VolumeTitle}（{_continueReadingItem.CurrentPageText}）";
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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
    public VolumeCardViewModel(long volumeId, long seriesId, string title, int pageCount, DateTimeOffset createdAt)
    {
        VolumeId = volumeId;
        SeriesId = seriesId;
        Title = title;
        PageCount = pageCount;
        CreatedAt = createdAt;
    }

    public long VolumeId { get; }

    public long SeriesId { get; }

    public string Title { get; }

    public int PageCount { get; }

    public DateTimeOffset CreatedAt { get; }

    public string CreatedAtText => CreatedAt.ToLocalTime().ToString("yyyy-MM-dd");
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