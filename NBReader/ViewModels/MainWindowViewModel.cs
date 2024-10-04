using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NBReader.Core;
using SharpCompress;
using SharpCompress.Readers;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NBReader.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string? _MangaFolder;
        public Interaction<string, string?> SelectZipFileInteraction { get; } = new Interaction<string, string?>();
        public MainWindowViewModel()
        {
            //
        }

        [RelayCommand]
        private async Task SelectZipFileAsync()
        {
            string? fullpath = await SelectZipFileInteraction.HandleAsync("选择zip漫画");
            string? zipFilepath = fullpath?.Replace("file:///", "");
            System.Diagnostics.Debug.WriteLine($"SelectedFile: {zipFilepath}");
            try
            {
                using (Stream stream = File.OpenRead(zipFilepath ?? ""))
                using (var reader = ReaderFactory.Open(stream))
                {
                    Directory.CreateDirectory("./extract");
                    reader.WriteAllToDirectory("./extract", new SharpCompress.Common.ExtractionOptions()
                    {
                        ExtractFullPath = true,
                        PreserveFileTime = true,
                        PreserveAttributes = true,
                    });
                    MangaFolder = zipFilepath;
                }
            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }
    }
}
