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
}
