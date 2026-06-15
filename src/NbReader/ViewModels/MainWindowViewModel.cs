using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NbReader.Core.Abstractions;
using NbReader.Core.Services;
using SharpCompress.Common;

namespace NbReader.ViewModels;

/// <summary>
/// 导航目标视图。
/// </summary>
public enum NavTarget
{
    Library,
    History,
    Reader
}

/// <summary>
/// 主窗口视图模型：管理全局导航和应用状态。
/// </summary>
public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IStorageService _storage;
    private readonly IComicInfoParser _comicInfoParser;

    /// <summary>
    /// 当前激活的视图模型。
    /// </summary>
    [ObservableProperty]
    private ViewModelBase _currentView;

    /// <summary>
    /// 当前选中的导航项。
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsReaderActive))]
    [NotifyPropertyChangedFor(nameof(IsLibraryActive))]
    [NotifyPropertyChangedFor(nameof(IsHistoryActive))]
    [NotifyPropertyChangedFor(nameof(WindowTitle))]
    private NavTarget _selectedNav = NavTarget.Reader;

    /// <summary>
    /// 是否在阅读器视图。
    /// </summary>
    public bool IsReaderActive => SelectedNav == NavTarget.Reader;

    /// <summary>
    /// 是否在书架视图。
    /// </summary>
    public bool IsLibraryActive => SelectedNav == NavTarget.Library;

    /// <summary>
    /// 是否在历史视图。
    /// </summary>
    public bool IsHistoryActive => SelectedNav == NavTarget.History;

    /// <summary>
    /// 窗口标题：阅读器显示漫画名，书架/历史显示导航名。
    /// </summary>
    public string WindowTitle
    {
        get
        {
            if (SelectedNav == NavTarget.Reader && !string.IsNullOrEmpty(Reader.ComicName))
                return $"NbReader - {Reader.ComicName}";

            return SelectedNav switch
            {
                NavTarget.Library => "NbReader - 书架",
                NavTarget.History => "NbReader - 阅读历史",
                _ => "NbReader"
            };
        }
    }

    /// <summary>
    /// 阅读器视图模型（常驻）。
    /// </summary>
    public ReaderViewModel Reader { get; }

    /// <summary>
    /// 书架视图模型（常驻）。
    /// </summary>
    public LibraryViewModel Library { get; }

    /// <summary>
    /// 历史视图模型（常驻）。
    /// </summary>
    public HistoryViewModel History { get; }

    public MainWindowViewModel(ReaderViewModel reader, IStorageService storage,
        IComicInfoParser comicInfoParser, LibraryScanner libraryScanner)
    {
        _storage = storage;
        _comicInfoParser = comicInfoParser;
        Reader = reader;
        Library = new LibraryViewModel(storage, NavigateToReader, libraryScanner);
        History = new HistoryViewModel(storage, NavigateToReader);
        CurrentView = reader;
    }

    /// <summary>
    /// 导航到阅读器（回到正在阅读的漫画）。
    /// </summary>
    [RelayCommand]
    private void NavigateToReader()
    {
        SelectedNav = NavTarget.Reader;
    }

    /// <summary>
    /// 导航到书架。
    /// </summary>
    [RelayCommand]
    private void NavigateToLibrary()
    {
        Reader.SaveCurrentProgress();
        SelectedNav = NavTarget.Library;
        Library.Refresh();
        CurrentView = Library;
    }

    /// <summary>
    /// 导航到历史。
    /// </summary>
    [RelayCommand]
    private void NavigateToHistory()
    {
        Reader.SaveCurrentProgress();
        SelectedNav = NavTarget.History;
        History.Refresh();
        CurrentView = History;
    }

    /// <summary>
    /// 由子 ViewModel 回调，用于从书架/历史打开漫画。
    /// </summary>
    private void NavigateToReader(string path)
    {
        SelectedNav = NavTarget.Reader;
        _ = OpenFileAsync(path);
    }

    /// <summary>
    /// 打开漫画文件（由 View 层调用，传入文件/文件夹路径）。
    /// </summary>
    public async Task OpenFileAsync(string path)
    {
        try
        {
            var fileSource = FileSourceFactory.Create(path);

            // 检测空文件源（压缩包中无图片）
            if (fileSource.PageCount == 0)
            {
                fileSource.Dispose();
                Reader.StatusText = "❌ 文件中没有找到图片";
                return;
            }

            await Reader.LoadFileSourceAsync(fileSource);
            SelectedNav = NavTarget.Reader;
            CurrentView = Reader;

            // 自动加入书架 + 恢复阅读进度
            var resourceId = RegisterInLibrary(path, fileSource.PageCount);
            await Reader.SetCurrentResourceAsync(resourceId);
        }
        catch (DirectoryNotFoundException)
        {
            Reader.StatusText = "❌ 目录不存在，请检查路径";
        }
        catch (FileNotFoundException)
        {
            Reader.StatusText = "❌ 文件不存在，请检查路径";
        }
        catch (NotSupportedException ex)
        {
            Reader.StatusText = $"❌ 不支持的格式: {ex.Message}";
        }
        catch (SharpCompress.Common.ArchiveException)
        {
            Reader.StatusText = "❌ 压缩包损坏或格式无效";
        }
        catch (Exception ex)
        {
            Reader.StatusText = $"❌ 打开失败: {ex.Message}";
        }
    }

    /// <summary>
    /// 将打开的文件自动注册到书架（若尚未入库），并记录阅读历史。
    /// 返回资源 ID。
    /// </summary>
    private int RegisterInLibrary(string path, int pageCount)
    {
        int resourceId = -1;
        try
        {
            var existing = _storage.GetResourceBySource("local", path);

            if (existing is not null)
            {
                existing.LastReadDate = DateTime.UtcNow.ToString("O");
                _storage.UpdateResource(existing);
                resourceId = existing.Id;
            }
            else
            {
                var title = Path.GetFileNameWithoutExtension(path);

                if (path.EndsWith(".cbz", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    var comicInfo = _comicInfoParser.ParseFromCbz(path);
                    if (comicInfo is not null)
                        title = comicInfo.Title ?? title;
                }

                var resource = new NbReader.Core.Models.ComicResource
                {
                    Title = title,
                    SourceType = "local",
                    SourceId = path,
                    LocalPath = path,
                    PageCount = pageCount,
                    AddedDate = DateTime.UtcNow.ToString("O"),
                    LastReadDate = DateTime.UtcNow.ToString("O"),
                    IsBookmarked = true,
                    IsDownloaded = true
                };

                resourceId = _storage.AddResource(resource);
            }

            // 记录阅读历史
            _storage.AddHistory(resourceId, 0);
            return resourceId;
        }
        catch
        {
            // 入库失败不影响阅读
            return -1;
        }
    }

    /// <summary>
    /// [验证用] 加载演示图片，验证渲染引擎。
    /// </summary>
    [RelayCommand]
    private async Task LoadDemoAsync()
    {
        await Reader.LoadDemoImageAsync();
        SelectedNav = NavTarget.Reader;
        CurrentView = Reader;
    }
}
