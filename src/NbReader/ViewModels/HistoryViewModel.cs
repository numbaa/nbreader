using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NbReader.Core.Abstractions;
using NbReader.Core.Models;

namespace NbReader.ViewModels;

/// <summary>
/// 阅读历史视图模型。
/// </summary>
public partial class HistoryViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly Action<string> _openComicCallback;

    /// <summary>按日期分组的历史记录</summary>
    public ObservableCollection<HistoryGroup> Groups { get; } = new();

    public HistoryViewModel(IStorageService storage, Action<string> openComicCallback)
    {
        _storage = storage;
        _openComicCallback = openComicCallback;
    }

    /// <summary>
    /// 刷新历史记录。
    /// </summary>
    public void Refresh()
    {
        var entries = _storage.GetHistory(limit: 200);
        Groups.Clear();

        var grouped = entries
            .GroupBy(e => GetDateGroup(e.ReadDate))
            .OrderByDescending(g => g.Key);

        foreach (var group in grouped)
        {
            Groups.Add(new HistoryGroup
            {
                DateLabel = group.Key,
                Entries = new ObservableCollection<ReadHistoryEntry>(group)
            });
        }
    }

    /// <summary>
    /// 点击历史条目继续阅读。
    /// </summary>
    [RelayCommand]
    private void OpenEntry(ReadHistoryEntry entry)
    {
        // 通过 resource 获取路径
        var resource = _storage.GetResource(entry.ResourceId);
        if (resource is null) return;

        var path = resource.IsDownloaded ? resource.LocalPath : resource.SourceId;
        if (path is not null)
            _openComicCallback(path);
    }

    /// <summary>
    /// 删除单条历史。
    /// </summary>
    [RelayCommand]
    private void DeleteEntry(ReadHistoryEntry entry)
    {
        _storage.DeleteHistory(entry.Id);
        Refresh();
    }

    /// <summary>
    /// 清除全部历史。
    /// </summary>
    [RelayCommand]
    private void ClearAllHistory()
    {
        _storage.ClearHistory();
        Groups.Clear();
    }

    /// <summary>
    /// 将日期字符串分组为"今天"/"昨天"/"更早"。
    /// </summary>
    private static string GetDateGroup(string isoDate)
    {
        if (!DateTime.TryParse(isoDate, out var date))
            return "更早";

        var today = DateTime.Today;
        if (date.Date == today) return "今天";
        if (date.Date == today.AddDays(-1)) return "昨天";
        if (date.Date > today.AddDays(-7)) return "本周";
        if (date.Date > today.AddDays(-30)) return "本月";
        return date.ToString("yyyy年M月");
    }
}

/// <summary>
/// 阅读历史分组。
/// </summary>
public class HistoryGroup
{
    public string DateLabel { get; set; } = string.Empty;
    public ObservableCollection<ReadHistoryEntry> Entries { get; set; } = new();
}
