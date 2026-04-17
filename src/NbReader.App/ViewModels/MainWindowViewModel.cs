using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;
using NbReader.Catalog;
using NbReader.Infrastructure;
using NbReader.Reader;

namespace NbReader.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
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
    private VolumeReaderContext? _activeReaderContext;
    private ReaderState _readerState = ReaderState.Empty;
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

    public string Subtitle => "书架与阅读闭环 - M3（单页阅读与基础预加载）";

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

    public string ReaderPageIndicator => _readerState.TotalPages == 0
        ? "0 / 0"
        : $"{_readerState.CurrentPageNumber} / {_readerState.TotalPages}";

    public bool CanGoToPreviousPage => _readerState.CanMovePrevious;

    public bool CanGoToNextPage => _readerState.CanMoveNext;

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

        var items = await Runtime.SeriesQueryService.GetSeriesListAsync(cancellationToken: cancellationToken);

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
    }

    public async Task OpenSelectedVolumeAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedSeries is null || SelectedVolume is null)
        {
            StatusMessage = "请先选择系列与卷。";
            return;
        }

        var context = await Runtime.VolumeQueryService.GetVolumeReaderContextAsync(SelectedVolume.VolumeId, cancellationToken);
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

        if (!NavigateToPage(0))
        {
            StatusMessage = "打开卷失败：无法读取第一页。";
            return;
        }

        NavigateTo("Reader");
        SelectedNavigation = "Reader";
    }

    public void ShowPreviousPage()
    {
        if (!CanGoToPreviousPage)
        {
            return;
        }

        NavigateToPage(_readerState.CurrentPageIndex - 1);
    }

    public void ShowNextPage()
    {
        if (!CanGoToNextPage)
        {
            return;
        }

        NavigateToPage(_readerState.CurrentPageIndex + 1);
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

    private bool NavigateToPage(int targetPageIndex)
    {
        if (_activeReaderContext is null)
        {
            return false;
        }

        var loadingState = ReaderStateMachine.NavigateTo(_readerState, targetPageIndex);
        SetReaderState(loadingState);

        if (!TryGetOrLoadPageBitmap(loadingState.CurrentPageIndex, out var bitmap))
        {
            SetReaderState(ReaderStateMachine.MarkError(loadingState, "读取页面失败"));
            StatusMessage = "读取页面失败，请确认资源可访问。";
            return false;
        }

        ReaderPreviewImage = bitmap;
        SetReaderState(ReaderStateMachine.MarkPageReady(loadingState));
        UpdateReaderLaunchSummary();
        PreloadAndReleaseAroundCurrentPage();

        if (_readerState.TotalPages > 0)
        {
            StatusMessage = $"阅读中：第 {_readerState.CurrentPageNumber} / {_readerState.TotalPages} 页。";
        }

        return true;
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

    private void PreloadAndReleaseAroundCurrentPage()
    {
        if (_activeReaderContext is null || _readerState.TotalPages == 0)
        {
            return;
        }

        var keepIndices = _preloadPolicy.GetWindowIndices(_readerState.CurrentPageIndex, _readerState.TotalPages);

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

        foreach (var bitmap in _pageBitmapCache.Values)
        {
            bitmap.Dispose();
        }

        _pageBitmapCache.Clear();
        _activeReaderContext = null;
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

    private void UpdateReaderLaunchSummary()
    {
        if (SelectedSeries is null || SelectedVolume is null || _readerState.TotalPages == 0)
        {
            return;
        }

        ReaderLaunchSummary =
            $"已从系列“{SelectedSeries.Title}”打开卷“{SelectedVolume.Title}”，共 {_readerState.TotalPages} 页，当前显示第 {_readerState.CurrentPageNumber} 页。";
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