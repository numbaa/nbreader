using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NBReader.Core;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace NBReader.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string? _MangaFolder;

        [ObservableProperty]
        private List<string>? _MangaFiles;

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
            if (zipFilepath == null)
            {
                return;
            }
            try
            {
                var guid = Guid.NewGuid().ToString();
                var tempFolder = Path.GetTempPath();
                var targetFoler = tempFolder + "NBReader/" + guid;
                await Task.Run(() =>
                {
                    using (Stream stream = File.OpenRead(zipFilepath ?? ""))
                    using (var reader = ReaderFactory.Open(stream))
                    {

                        Directory.CreateDirectory(targetFoler);
                        reader.WriteAllToDirectory(targetFoler, new SharpCompress.Common.ExtractionOptions()
                        {
                            ExtractFullPath = true,
                            PreserveFileTime = true,
                            PreserveAttributes = true,
                        });
                        MangaFolder = targetFoler;
                        MangaFiles = Directory.GetFiles(targetFoler).ToList();
                    }
                });

            }
            catch(Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error: {ex}");
            }
        }
    }
}
