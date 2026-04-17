using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SharpCompress.Archives;

namespace NbReader.Import;

public static class ImportModule
{
	public const string Name = "Import";
}

public sealed record ZipImageEntry(string EntryPath, long Size);

public sealed class ZipImageEnumerator
{
	public IReadOnlyList<ZipImageEntry> Enumerate(string zipFilePath)
	{
		if (string.IsNullOrWhiteSpace(zipFilePath) || !File.Exists(zipFilePath))
		{
			return [];
		}

		using var archive = ArchiveFactory.Open(zipFilePath);
		var entries = archive.Entries
			.Where(entry => !entry.IsDirectory)
			.Where(entry => IsImageFile(entry.Key))
			.Select(entry => new ZipImageEntry(entry.Key ?? string.Empty, entry.Size))
			.OrderBy(entry => entry.EntryPath, NaturalStringComparer.Instance)
			.ToArray();

		return entries;
	}

	private static bool IsImageFile(string? key)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return false;
		}

		var extension = Path.GetExtension(key);
		return ImageFileRules.IsSupportedImageExtension(extension);
	}
}

internal sealed class NaturalStringComparer : IComparer<string>
{
	public static NaturalStringComparer Instance { get; } = new();

	public int Compare(string? x, string? y)
	{
		x ??= string.Empty;
		y ??= string.Empty;

		var xParts = Tokenize(x);
		var yParts = Tokenize(y);
		var count = xParts.Count < yParts.Count ? xParts.Count : yParts.Count;

		for (var i = 0; i < count; i++)
		{
			var left = xParts[i];
			var right = yParts[i];

			var leftNumber = long.TryParse(left, NumberStyles.None, CultureInfo.InvariantCulture, out var leftValue);
			var rightNumber = long.TryParse(right, NumberStyles.None, CultureInfo.InvariantCulture, out var rightValue);

			int result;
			if (leftNumber && rightNumber)
			{
				result = leftValue.CompareTo(rightValue);
			}
			else
			{
				result = string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
			}

			if (result != 0)
			{
				return result;
			}
		}

		return xParts.Count.CompareTo(yParts.Count);
	}

	private static List<string> Tokenize(string value)
	{
		var result = new List<string>();
		if (string.IsNullOrEmpty(value))
		{
			return result;
		}

		var current = new List<char>();
		var isDigit = char.IsDigit(value[0]);

		foreach (var c in value)
		{
			var digit = char.IsDigit(c);
			if (digit != isDigit && current.Count > 0)
			{
				result.Add(new string(current.ToArray()));
				current.Clear();
				isDigit = digit;
			}

			current.Add(c);
		}

		if (current.Count > 0)
		{
			result.Add(new string(current.ToArray()));
		}

		return result;
	}
}
