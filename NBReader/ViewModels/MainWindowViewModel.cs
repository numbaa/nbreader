﻿using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NBReader.Core;
using NBReader.Models;
using SharpCompress.Readers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace NBReader.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        [ObservableProperty]
        private string? _MangaFolder;

        [ObservableProperty]
        private List<MangaFile>? _MangaFiles;

        [ObservableProperty]
        private HomeViewModel? _HomeViewModel;

        public Interaction<string, string?> SelectZipFileInteraction { get; } = new Interaction<string, string?>();
        public MainWindowViewModel()
        {
            HomeViewModel = new HomeViewModel();
        }

        [RelayCommand]
        private async Task SelectZipFileAsync()
        {
            string? fullpath = await SelectZipFileInteraction.HandleAsync("选择zip漫画");
            string? zipFilepath;
            bool isWindows = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            if (isWindows) {
                zipFilepath = fullpath?.Replace("file:///", "");
            } else {
                zipFilepath = fullpath?.Replace("file://", "");
            }
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
                            //ExtractFullPath = true,
                            //PreserveFileTime = true,
                            //PreserveAttributes = true,
                        });
                        MangaFolder = targetFoler;
                        var mangaFiles = new List<MangaFile>();
                        foreach (var file in Directory.GetFiles(targetFoler))
                        {
                            var filePath = file.Replace('\\', '/');
                            var fileStream = File.OpenRead(filePath);
                            var bitmap = Bitmap.DecodeToWidth(fileStream, 600);
                            mangaFiles.Add(new MangaFile(filePath, bitmap));
                        }
                        MangaFiles = mangaFiles;
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
