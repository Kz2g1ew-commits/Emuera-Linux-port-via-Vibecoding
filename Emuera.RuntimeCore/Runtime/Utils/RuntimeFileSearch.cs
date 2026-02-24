using System;
using System.IO;

namespace MinorShift.Emuera.Runtime.Utils;

internal static class RuntimeFileSearch
{
	public static string[] GetFiles(string path, string searchPattern, SearchOption searchOption)
	{
		var resolvedPath = ResolveDirectoryPath(path);
		if (Directory.Exists(resolvedPath))
			path = resolvedPath;
		var enumerationOptions = new EnumerationOptions
		{
			RecurseSubdirectories = searchOption == SearchOption.AllDirectories,
			MatchCasing = MatchCasing.CaseInsensitive,
			MatchType = MatchType.Win32,
		};
		return Directory.GetFiles(path, searchPattern, enumerationOptions);
	}

	public static string ResolveFilePath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return path;
		if (File.Exists(path))
			return path;

		var directory = Path.GetDirectoryName(path);
		var fileName = Path.GetFileName(path);
		if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
			return path;

		directory = ResolveDirectoryPath(directory);
		if (!Directory.Exists(directory))
			return path;

		foreach (var candidate in Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly))
		{
			if (string.Equals(Path.GetFileName(candidate), fileName, StringComparison.OrdinalIgnoreCase))
				return candidate;
		}

		return path;
	}

	public static string ResolveDirectoryPath(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return path;
		if (Directory.Exists(path))
			return path;

		var parent = Path.GetDirectoryName(path);
		var dirName = Path.GetFileName(path);
		if (string.IsNullOrWhiteSpace(parent) || string.IsNullOrWhiteSpace(dirName))
			return path;

		parent = ResolveDirectoryPath(parent);
		if (!Directory.Exists(parent))
			return path;

		foreach (var candidate in Directory.EnumerateDirectories(parent, "*", SearchOption.TopDirectoryOnly))
		{
			if (string.Equals(Path.GetFileName(candidate), dirName, StringComparison.OrdinalIgnoreCase))
				return candidate;
		}

		return path;
	}
}
