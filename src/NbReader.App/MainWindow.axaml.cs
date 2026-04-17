using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using NbReader.App.ViewModels;
using NbReader.Import;
using NbReader.Infrastructure;
using NbReader.Reader;

namespace NbReader.App;

public partial class MainWindow : Window
{
    private AppRuntime? _runtime;
    private readonly DirectoryPageSource _directoryPageSource = new();
    private readonly ZipImageEnumerator _zipImageEnumerator = new();
    private Bitmap? _currentPreviewBitmap;

    public MainWindow()
    {
        InitializeComponent();
    }

    public MainWindow(AppRuntime runtime)
        : this()
    {
        _runtime = runtime;
        DataContext = new MainWindowViewModel(runtime);
    }

    private async void OnPickDirectoryClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (StorageProvider is null)
        {
            SetActionStatus("当前平台不支持文件选择器。", true);
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择图片目录",
            AllowMultiple = false,
        });

        var selectedFolder = folders.FirstOrDefault();
        if (selectedFolder is null)
        {
            SetActionStatus("已取消目录选择。", false);
            return;
        }

        var folderPath = selectedFolder.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            SetActionStatus("无法读取所选目录路径。", true);
            return;
        }

        try
        {
            var runtime = _runtime;
            if (runtime is null)
            {
                SetActionStatus("运行时未初始化。", true);
                return;
            }

            var pages = _directoryPageSource.EnumeratePages(folderPath);
            if (pages.Count == 0)
            {
                SetPreviewImage(null);
                DirectorySummaryText.Text = $"目录 {folderPath} 中未找到可用图片。";
                DatabaseSummaryText.Text = "数据库写入状态：未执行。";
                SetActionStatus("目录读取完成，但没有可显示图片。", false);
                runtime.Logger.Info($"Directory selected but no image found: {folderPath}");
                return;
            }

            var firstPage = pages[0];
            using var stream = File.OpenRead(firstPage);
            SetPreviewImage(new Bitmap(stream));

            DirectorySummaryText.Text =
                $"目录: {folderPath}{Environment.NewLine}" +
                $"图片总数: {pages.Count}{Environment.NewLine}" +
                $"第一页: {Path.GetFileName(firstPage)}";

            var result = runtime.Database.UpsertSampleData(
                folderPath,
                "directory",
                AppDatabase.BuildDefaultVolumeTitle(folderPath),
                pages.ToArray());
            var storedPageCount = runtime.Database.ReadPageCountByVolume(result.VolumeId);
            DatabaseSummaryText.Text =
                $"数据库写入状态：Source #{result.SourceId}, Volume #{result.VolumeId}, 写入页数 {result.InsertedPageRows}, 当前存储页数 {storedPageCount}";

            SetActionStatus($"目录验证完成，已显示第一页并写入 {result.InsertedPageRows} 条测试页面。", false);
            runtime.Logger.Info($"Directory validated: {folderPath}, pages={pages.Count}");
        }
        catch (Exception ex)
        {
            _runtime?.Logger.Error($"Directory validation failed: {folderPath}", ex);
            SetActionStatus($"目录验证失败: {ex.Message}", true);
            DirectorySummaryText.Text = "目录读取失败，请查看日志。";
            DatabaseSummaryText.Text = "数据库写入状态：失败。";
        }
    }

    private async void OnPickZipClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (StorageProvider is null)
        {
            SetActionStatus("当前平台不支持文件选择器。", true);
            return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择 zip 文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("ZIP")
                {
                    Patterns = ["*.zip"],
                    MimeTypes = ["application/zip"],
                },
            ],
        });

        var selectedFile = files.FirstOrDefault();
        if (selectedFile is null)
        {
            SetActionStatus("已取消 zip 选择。", false);
            return;
        }

        var zipPath = selectedFile.TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(zipPath))
        {
            SetActionStatus("无法读取所选 zip 路径。", true);
            return;
        }

        try
        {
            var runtime = _runtime;
            if (runtime is null)
            {
                SetActionStatus("运行时未初始化。", true);
                return;
            }

            var entries = _zipImageEnumerator.Enumerate(zipPath);
            if (entries.Count == 0)
            {
                ZipSummaryText.Text = $"zip: {zipPath}{Environment.NewLine}未找到图片条目。";
                DatabaseSummaryText.Text = "数据库写入状态：未执行。";
                SetActionStatus("zip 枚举完成，但没有可用图片条目。", false);
                runtime.Logger.Info($"Zip selected but no image entry found: {zipPath}");
                return;
            }

            var previewNames = string.Join(Environment.NewLine, entries.Take(3).Select(x => x.EntryPath));
            ZipSummaryText.Text =
                $"zip: {zipPath}{Environment.NewLine}" +
                $"图片条目数: {entries.Count}{Environment.NewLine}" +
                $"前 3 条:{Environment.NewLine}{previewNames}";

            var pageLocators = entries.Select(x => x.EntryPath).ToArray();
            var result = runtime.Database.UpsertSampleData(
                zipPath,
                "zip",
                AppDatabase.BuildDefaultVolumeTitle(zipPath),
                pageLocators);
            var storedPageCount = runtime.Database.ReadPageCountByVolume(result.VolumeId);
            DatabaseSummaryText.Text =
                $"数据库写入状态：Source #{result.SourceId}, Volume #{result.VolumeId}, 写入页数 {result.InsertedPageRows}, 当前存储页数 {storedPageCount}";

            SetActionStatus($"zip 枚举验证完成，已写入 {result.InsertedPageRows} 条测试页面。", false);
            runtime.Logger.Info($"Zip validated: {zipPath}, entries={entries.Count}");
        }
        catch (Exception ex)
        {
            _runtime?.Logger.Error($"Zip validation failed: {zipPath}", ex);
            SetActionStatus($"zip 验证失败: {ex.Message}", true);
            ZipSummaryText.Text = "zip 枚举失败，请查看日志。";
            DatabaseSummaryText.Text = "数据库写入状态：失败。";
        }
    }

    private void SetActionStatus(string message, bool isError)
    {
        ActionStatusText.Text = message;
        ActionStatusText.Foreground = isError
            ? Avalonia.Media.Brushes.Firebrick
            : Avalonia.Media.Brushes.DarkSlateBlue;
    }

    private void SetPreviewImage(Bitmap? bitmap)
    {
        var old = _currentPreviewBitmap;
        _currentPreviewBitmap = bitmap;
        PreviewImage.Source = _currentPreviewBitmap;
        old?.Dispose();
    }
}