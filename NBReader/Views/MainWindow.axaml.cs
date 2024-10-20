using Avalonia.Controls;
using NBReader.ViewModels;
using System;
using System.Threading.Tasks;

namespace NBReader.Views
{
    public partial class MainWindow : Window
    {
        private IDisposable? _selectZipFileInteractionDisposable;
        public MainWindow()
        {
            InitializeComponent();
            System.Diagnostics.Debug.WriteLine("Construct MainWindow");
        }

        protected override void OnDataContextChanged(EventArgs e)
        {
            _selectZipFileInteractionDisposable?.Dispose();
            if (this.DataContext is MainWindowViewModel vm)
            {
                System.Diagnostics.Debug.WriteLine("DataContext is MainWindowViewModel");
                vm.SelectZipFileInteraction.RegisterHandler(SelectZipFileHandler);
            }
            base.OnDataContextChanged(e);
        }

        private async Task<string?> SelectZipFileHandler(string input)
        {
            var file = await this.StorageProvider.OpenFilePickerAsync(
                new Avalonia.Platform.Storage.FilePickerOpenOptions() {
                    AllowMultiple = false,
                    Title = input,
                });
            if (file != null && file.Count > 0)
            {
                return file[0].Path.ToString();
            }
            else
            {
                return null;
            }
        }
    }
}