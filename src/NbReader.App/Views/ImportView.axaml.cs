using Avalonia.Controls;
using Avalonia.Platform.Storage;
using NbReader.App.ViewModels;

namespace NbReader.App.Views;

public partial class ImportView : UserControl
{
    public ImportView()
    {
        InitializeComponent();
    }

    private async void OnImportPickFolderClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ImportSectionViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            viewModel.ReportStatus("当前环境不支持路径选择器。请手动输入路径。");
            return;
        }

        try
        {
            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择漫画目录",
                AllowMultiple = false,
            });

            var selected = folders.FirstOrDefault();
            if (selected is null)
            {
                return;
            }

            var localPath = selected.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                viewModel.SetImportPathFromPicker(localPath);
            }
        }
        catch (Exception ex)
        {
            viewModel.ReportStatus($"选择目录失败：{ex.Message}");
            viewModel.Runtime.Logger.Error("Failed to pick import folder.", ex);
        }
    }

    private async void OnImportPickZipClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DataContext is not ImportSectionViewModel viewModel)
        {
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel?.StorageProvider is null)
        {
            viewModel.ReportStatus("当前环境不支持文件选择器。请手动输入路径。");
            return;
        }

        try
        {
            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择 zip 文件",
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new FilePickerFileType("ZIP")
                    {
                        Patterns = ["*.zip"],
                    },
                ],
            });

            var selected = files.FirstOrDefault();
            if (selected is null)
            {
                return;
            }

            var localPath = selected.TryGetLocalPath();
            if (!string.IsNullOrWhiteSpace(localPath))
            {
                viewModel.SetImportPathFromPicker(localPath);
            }
        }
        catch (Exception ex)
        {
            viewModel.ReportStatus($"选择 zip 失败：{ex.Message}");
            viewModel.Runtime.Logger.Error("Failed to pick import zip.", ex);
        }
    }
}
