using System.Collections.ObjectModel;
using System.Windows.Input;

namespace NbReader.App.ViewModels;

public sealed class CatalogSectionViewModel : SectionViewModelBase
{
    public CatalogSectionViewModel(MainWindowViewModel owner)
        : base(owner)
    {
    }

    public string StatusMessage => Owner.StatusMessage;

    public bool IsCatalogEmpty => Owner.IsCatalogEmpty;

    public ICommand GoToImportCommand => Owner.GoToImportCommand;

    public ObservableCollection<SeriesCardViewModel> SeriesCards => Owner.SeriesCards;

    public SeriesCardViewModel? SelectedSeries
    {
        get => Owner.SelectedSeries;
        set => Owner.SelectedSeries = value;
    }

    public string SeriesDetailTitle => Owner.SeriesDetailTitle;

    public ObservableCollection<VolumeCardViewModel> VolumeCards => Owner.VolumeCards;

    public VolumeCardViewModel? SelectedVolume
    {
        get => Owner.SelectedVolume;
        set => Owner.SelectedVolume = value;
    }

    public bool CanOpenSelectedVolume => Owner.CanOpenSelectedVolume;

    public ICommand OpenSelectedVolumeCommand => Owner.OpenSelectedVolumeCommand;

    public bool CanContinueReading => Owner.CanContinueReading;

    public ICommand OpenContinueReadingCommand => Owner.OpenContinueReadingCommand;

    public bool IsLoadingVolumes => Owner.IsLoadingVolumes;

    public string CatalogPrimaryActionHint => Owner.CatalogPrimaryActionHint;

    public bool ShouldShowCatalogPrimaryActionHint => Owner.ShouldShowCatalogPrimaryActionHint;

    public string ContinueReadingActionHint => Owner.ContinueReadingActionHint;

    public bool ShouldShowContinueReadingActionHint => Owner.ShouldShowContinueReadingActionHint;

    public string ContinueReadingSummary => Owner.ContinueReadingSummary;

    public ObservableCollection<RecentReadingItemViewModel> RecentReadings => Owner.RecentReadings;

    public RecentReadingItemViewModel? SelectedRecentReading
    {
        get => Owner.SelectedRecentReading;
        set => Owner.SelectedRecentReading = value;
    }

    public bool CanOpenSelectedRecentReading => Owner.CanOpenSelectedRecentReading;

    public ICommand OpenSelectedRecentReadingCommand => Owner.OpenSelectedRecentReadingCommand;
}
