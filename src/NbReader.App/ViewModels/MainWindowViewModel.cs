using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using NbReader.Catalog;
using NbReader.Infrastructure;

namespace NbReader.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string _currentSection = "Catalog";
    private string _selectedNavigation = "Catalog";
    private string _statusMessage = "正在加载系列列表...";
    private SeriesCardViewModel? _selectedSeries;

    public MainWindowViewModel(AppRuntime runtime)
    {
        Runtime = runtime;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public AppRuntime Runtime { get; }

    public string Title => "NbReader";

    public string Subtitle => "书架与阅读闭环 - M1（导航框架与系列聚合查询）";

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
        }
    }

    public bool IsCatalogSection => string.Equals(CurrentSection, CatalogModule.Name, StringComparison.OrdinalIgnoreCase);
    public bool IsPlaceholderSection => !IsCatalogSection;

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
                StatusMessage = $"已选择系列：{_selectedSeries.Title}（{_selectedSeries.VolumeCount} 卷）";
            }
        }
    }

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
        if (!IsCatalogSection)
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