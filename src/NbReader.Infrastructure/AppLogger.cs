using System;
using System.IO;

namespace NbReader.Infrastructure;

public sealed class AppLogger
{
    private readonly object _syncRoot = new();

    public string LogFilePath { get; }

    public AppLogger(string logFilePath)
    {
        LogFilePath = logFilePath;
    }

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Error(string message, Exception exception)
    {
        Write("ERROR", $"{message}{Environment.NewLine}{exception}");
    }

    private void Write(string level, string message)
    {
        var directory = Path.GetDirectoryName(LogFilePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var line = $"[{DateTimeOffset.Now:O}] [{level}] {message}{Environment.NewLine}";
        lock (_syncRoot)
        {
            File.AppendAllText(LogFilePath, line);
        }
    }
}