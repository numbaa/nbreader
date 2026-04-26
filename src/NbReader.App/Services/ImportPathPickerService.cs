using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace NbReader.App.Services;

public interface IImportPathPickerService
{
    Task<string?> PickFolderAsync();

    Task<string?> PickZipAsync();
}

public sealed class AvaloniaImportPathPickerService : IImportPathPickerService
{
    private readonly Func<TopLevel?> _topLevelProvider;

    public AvaloniaImportPathPickerService(Func<TopLevel?> topLevelProvider)
    {
        _topLevelProvider = topLevelProvider ?? throw new ArgumentNullException(nameof(topLevelProvider));
    }

    public async Task<string?> PickFolderAsync()
    {
        var topLevel = _topLevelProvider();
        if (topLevel?.StorageProvider is null)
        {
            return null;
        }

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择漫画目录",
            AllowMultiple = false,
        });

        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickZipAsync()
    {
        var topLevel = _topLevelProvider();
        if (topLevel?.StorageProvider is null)
        {
            return null;
        }

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

        return files.FirstOrDefault()?.TryGetLocalPath();
    }
}
