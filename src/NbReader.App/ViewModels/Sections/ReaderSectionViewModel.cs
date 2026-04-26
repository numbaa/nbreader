using System.Windows.Input;
using Avalonia.Media.Imaging;

namespace NbReader.App.ViewModels;

public sealed class ReaderSectionViewModel : SectionViewModelBase
{
    public ReaderSectionViewModel(MainWindowViewModel owner)
        : base(owner)
    {
    }

    public string ReaderLaunchSummary => Owner.ReaderLaunchSummary;

    public string ReaderPageIndicator => Owner.ReaderPageIndicator;

    public string ReaderStateLabel => Owner.ReaderStateLabel;

    public string ReaderDisplayModeLabel => Owner.ReaderDisplayModeLabel;

    public string ReaderDirectionLabel => Owner.ReaderDirectionLabel;

    public ICommand ReaderToggleModeCommand => Owner.ReaderToggleModeCommand;

    public ICommand ReaderToggleDirectionCommand => Owner.ReaderToggleDirectionCommand;

    public bool CanGoToPreviousPage => Owner.CanGoToPreviousPage;

    public ICommand ReaderPreviousPageCommand => Owner.ReaderPreviousPageCommand;

    public bool CanGoToNextPage => Owner.CanGoToNextPage;

    public ICommand ReaderNextPageCommand => Owner.ReaderNextPageCommand;

    public bool IsSinglePageMode => Owner.IsSinglePageMode;

    public Bitmap? ReaderPreviewImage => Owner.ReaderPreviewImage;

    public bool IsDualPageMode => Owner.IsDualPageMode;

    public bool HasReaderLeftPageImage => Owner.HasReaderLeftPageImage;

    public Bitmap? ReaderLeftPageImage => Owner.ReaderLeftPageImage;

    public bool HasReaderRightPageImage => Owner.HasReaderRightPageImage;

    public Bitmap? ReaderRightPageImage => Owner.ReaderRightPageImage;
}
