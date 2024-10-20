using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NBReader.Models
{
    public class MangaFile
    {
        public string File { get; set; }

        public MangaFile(string file)
        {
            this.File = file;
        }
    }
}
