using System.Collections.Generic;

namespace NbReader.Infrastructure;

public sealed class AppSettings
{
    public string Theme { get; set; } = "system";

    public string ReaderDirection { get; set; } = "rtl";

    public string ZoomMode { get; set; } = "fit_width";

    public int PreloadPageCount { get; set; } = 5;

    public List<string> LibraryRoots { get; set; } = [];

    public bool EnableDiagnosticsOverlay { get; set; }
}