using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Material.Styles.Controls;

namespace NBReader.Views
{

    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
            DrawerList.PointerReleased += DrawerSelectionChanged;
        }

        private void DrawerSelectionChanged(object? sender, RoutedEventArgs? args)
        {
            if (!(sender is ListBox))
            {
                return;
            }
            var listBox = (ListBox)sender;
            if (!listBox.IsFocused)
            {
                //return; //?
            }
            try
            {
                PageCarousel.SelectedIndex = listBox.SelectedIndex;
            }
            catch
            {
                //
            }
            LeftDrawer.OptionalCloseLeftDrawer();
        }

        private void TemplatedControl_OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            SnackbarHost.Post("Welcom to NBReader!", null, DispatcherPriority.Normal);
        }

    }

}