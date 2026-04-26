using System.Collections.ObjectModel;
using System.Windows.Input;
using NbReader.Infrastructure;

namespace NbReader.App.ViewModels;

public sealed class ImportSectionViewModel : SectionViewModelBase
{
    public ImportSectionViewModel(MainWindowViewModel owner)
        : base(owner)
    {
    }

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
}
