using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;

namespace NBReader.Models
{
    public class MangaFile
    {
        public string Path { get; set; }

        public Bitmap Bitmap { get; set; }

        public MangaFile(string path, Bitmap bitmap)
        {
            this.Path = path;
            this.Bitmap = bitmap;
        }
    }
}
