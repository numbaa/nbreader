using System;
using System.IO;

namespace NbReader.Infrastructure;

public static class InfrastructureModule
{
	public const string Name = "Infrastructure";
}

public static class AppEnvironment
{
	public static string AppBaseDirectory { get; } = AppContext.BaseDirectory;

	public static string AppDataRoot { get; } = Path.Combine(
		Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
		"NbReader");

	public static string CacheRoot { get; } = Path.Combine(AppDataRoot, "cache");

	public static string ConfigRoot { get; } = Path.Combine(AppDataRoot, "config");

	public static string LogsRoot { get; } = Path.Combine(AppDataRoot, "logs");

	public static string DataRoot { get; } = Path.Combine(AppDataRoot, "data");

	public static string SettingsFilePath { get; } = Path.Combine(ConfigRoot, "settings.json");

	public static string DatabaseFilePath { get; } = Path.Combine(DataRoot, "nbreader.db");

	public static string CurrentLogFilePath { get; } = Path.Combine(LogsRoot, $"nbreader-{DateTime.Now:yyyyMMdd}.log");

	public static void EnsureCreatedDirectories()
	{
		Directory.CreateDirectory(AppDataRoot);
		Directory.CreateDirectory(CacheRoot);
		Directory.CreateDirectory(ConfigRoot);
		Directory.CreateDirectory(LogsRoot);
		Directory.CreateDirectory(DataRoot);
	}
}
