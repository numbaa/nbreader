using System.Collections.ObjectModel;
using System.Windows.Input;
using NbReader.App.Services;
using NbReader.Infrastructure;

namespace NbReader.App.ViewModels;

public sealed class ImportSectionViewModel : SectionViewModelBase
{
    private readonly IImportPathPickerService _pathPickerService;

    public ImportSectionViewModel(MainWindowViewModel owner, IImportPathPickerService pathPickerService)
        : base(owner)
    {
        _pathPickerService = pathPickerService ?? throw new ArgumentNullException(nameof(pathPickerService));
        PickFolderCommand = new AsyncDelegateCommand(PickFolderAsync);
        PickZipCommand = new AsyncDelegateCommand(PickZipAsync);
    }

    public ICommand PickFolderCommand { get; }

    public ICommand PickZipCommand { get; }

    public ObservableCollection<string> RecentImportPaths => Owner.RecentImportPaths;

    public string? SelectedRecentImportPath
    {
        get => Owner.SelectedRecentImportPath;
        set => Owner.SelectedRecentImportPath = value;
    }

    public string ImportPathInput
    {
        get => Owner.ImportPathInput;
        set => Owner.ImportPathInput = value;
    }

    public bool CanAnalyzeImportInput => Owner.CanAnalyzeImportInput;

    public ICommand AnalyzeImportInputCommand => Owner.AnalyzeImportInputCommand;

    public bool CanExecuteImport => Owner.CanExecuteImport;

    public ICommand ExecuteImportCommand => Owner.ExecuteImportCommand;

    public string ImportInputKindSummary => Owner.ImportInputKindSummary;

    public string ImportPlanSummary => Owner.ImportPlanSummary;

    public string ImportWarningsSummary => Owner.ImportWarningsSummary;

    public string ImportConflictsSummary => Owner.ImportConflictsSummary;

    public string ImportExecutionSummary => Owner.ImportExecutionSummary;

    public bool ImportRequiresConfirmation => Owner.ImportRequiresConfirmation;

    public string ImportSeriesNameOverride
    {
        get => Owner.ImportSeriesNameOverride;
        set => Owner.ImportSeriesNameOverride = value;
    }

    public bool ImportSkipDuplicateVolumes
    {
        get => Owner.ImportSkipDuplicateVolumes;
        set => Owner.ImportSkipDuplicateVolumes = value;
    }

    public bool ImportIgnoreWarnings
    {
        get => Owner.ImportIgnoreWarnings;
        set => Owner.ImportIgnoreWarnings = value;
    }

    public string StatusMessage => Owner.StatusMessage;

    public AppRuntime Runtime => Owner.Runtime;

    public void ReportStatus(string message)
    {
        Owner.ReportStatus(message);
    }

    public void SetImportPathFromPicker(string path)
    {
        Owner.SetImportPathFromPicker(path);
    }

    private async Task PickFolderAsync()
    {
        try
        {
            var selected = await _pathPickerService.PickFolderAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(selected))
            {
                ReportStatus("未选择目录。可继续手动输入路径。");
                return;
            }

            SetImportPathFromPicker(selected);
        }
        catch (Exception ex)
        {
            ReportStatus($"选择目录失败：{ex.Message}");
            Runtime.Logger.Error("Failed to pick import folder.", ex);
        }
    }

    private async Task PickZipAsync()
    {
        try
        {
            var selected = await _pathPickerService.PickZipAsync().ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(selected))
            {
                ReportStatus("未选择 zip。可继续手动输入路径。");
                return;
            }

            SetImportPathFromPicker(selected);
        }
        catch (Exception ex)
        {
            ReportStatus($"选择 zip 失败：{ex.Message}");
            Runtime.Logger.Error("Failed to pick import zip.", ex);
        }
    }
}
