using System.Collections.ObjectModel;
using System.Windows.Input;

namespace NbReader.App.ViewModels;

public sealed class SearchSectionViewModel : SectionViewModelBase
{
    public SearchSectionViewModel(MainWindowViewModel owner)
        : base(owner)
    {
    }

    public string SearchWorkspaceSummary => Owner.SearchWorkspaceSummary;

    public ICommand SearchRefreshCommand => Owner.SearchRefreshCommand;

    public bool CanRetrySelectedFailedImportTask => Owner.CanRetrySelectedFailedImportTask;

    public ICommand RetrySelectedFailedImportTaskCommand => Owner.RetrySelectedFailedImportTaskCommand;

    public bool IsLoadingSearchWorkspace => Owner.IsLoadingSearchWorkspace;

    public string SeriesRenameInput
    {
        get => Owner.SeriesRenameInput;
        set => Owner.SeriesRenameInput = value;
    }

    public bool CanApplySeriesRename => Owner.CanApplySeriesRename;

    public ICommand ApplySeriesRenameCommand => Owner.ApplySeriesRenameCommand;

    public ObservableCollection<SeriesMergeTargetItemViewModel> MergeTargetSeriesOptions => Owner.MergeTargetSeriesOptions;

    public SeriesMergeTargetItemViewModel? SelectedMergeTargetSeries
    {
        get => Owner.SelectedMergeTargetSeries;
        set => Owner.SelectedMergeTargetSeries = value;
    }

    public bool CanMergeSelectedSeries => Owner.CanMergeSelectedSeries;

    public ICommand MergeSelectedSeriesCommand => Owner.MergeSelectedSeriesCommand;

    public string VolumeNumberInput
    {
        get => Owner.VolumeNumberInput;
        set => Owner.VolumeNumberInput = value;
    }

    public bool CanApplyVolumeNumberCorrection => Owner.CanApplyVolumeNumberCorrection;

    public ICommand ApplyVolumeNumberCorrectionCommand => Owner.ApplyVolumeNumberCorrectionCommand;

    public string AuthorNamesInput
    {
        get => Owner.AuthorNamesInput;
        set => Owner.AuthorNamesInput = value;
    }

    public string TagNamesInput
    {
        get => Owner.TagNamesInput;
        set => Owner.TagNamesInput = value;
    }

    public bool CanApplySeriesMetadata => Owner.CanApplySeriesMetadata;

    public ICommand ApplySeriesMetadataCommand => Owner.ApplySeriesMetadataCommand;

    public ObservableCollection<UnorganizedVolumeItemViewModel> UnorganizedVolumes => Owner.UnorganizedVolumes;

    public ObservableCollection<FailedImportTaskItemViewModel> FailedImportTasks => Owner.FailedImportTasks;

    public FailedImportTaskItemViewModel? SelectedFailedImportTask
    {
        get => Owner.SelectedFailedImportTask;
        set => Owner.SelectedFailedImportTask = value;
    }
}
