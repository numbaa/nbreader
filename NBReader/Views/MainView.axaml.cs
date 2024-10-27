using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Material.Styles.Controls;

namespace NBReader.Views
{

    public partial class MainView : UserControl
    {
        public MainView()
        {
            InitializeComponent();
        }

        private void TemplatedControl_OnTemplateApplied(object? sender, TemplateAppliedEventArgs e)
        {
            SnackbarHost.Post("Welcom to NBReader!", null, DispatcherPriority.Normal);
        }

    }

}