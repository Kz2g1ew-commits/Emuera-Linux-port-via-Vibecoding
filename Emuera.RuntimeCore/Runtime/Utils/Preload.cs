using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MinorShift.Emuera.Runtime.Utils;
static partial class Preload
{
	static Dictionary<string, string[]> files = new(StringComparer.OrdinalIgnoreCase);
	public static Action<string, string> WarnHook { get; set; }
	public static int CachedFileCount => files.Count;
	static string EncodingWarning =>
		RuntimeHost.ResolveMessage("preload.abnormal_encode", "Encoding error. Please check file encoding (SJIS/UTF-8 recommended).");
	static string LockedFileWarningFormat =>
		RuntimeHost.ResolveMessage("preload.file_locked", "File: {0} is used by another process (loading with UTF-8 BOM fallback).");

	static void Warn(string message, string path)
	{
		if (WarnHook != null)
		{
			WarnHook(message, path);
			return;
		}
		RuntimeHost.ShowInfo(message);
	}

	public static string[] GetFileLines(string path)
	{
		return files[path];
	}

	public static bool TryGetFileLines(string path, out string[] lines)
	{
		lock (files)
		{
			return files.TryGetValue(path, out lines) && lines != null;
		}
	}

	public static string[] GetCachedFilePathsSnapshot()
	{
		lock (files)
		{
			return files.Keys.ToArray();
		}
	}

	static bool IsTargetExtension(string extension)
	{
		return extension.Equals(".csv", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".erb", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".erh", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".erd", StringComparison.OrdinalIgnoreCase) ||
			extension.Equals(".als", StringComparison.OrdinalIgnoreCase);
	}

	// Opens as UTF8BOM if starts with BOM, else use DetectEncoding
	private static string[] readAllLinesDetectEncoding(string path)
	{
		try
		{
			using var file = File.Open(path, FileMode.Open);
			Span<byte> bom = stackalloc byte[3];
			_ = file.Read(bom);
			file.Close();
			try
			{
				if (bom.SequenceEqual<byte>([0xEF, 0xBB, 0xBF]))
				{
					return File.ReadAllLines(path, EncodingHandler.UTF8BOMEncoding);
				}
				else
				{
					return File.ReadAllLines(path, EncodingHandler.DetectEncoding(path));
				}
			}
			catch
			{
				Warn(EncodingWarning, path);
				return null;
			}
		}
		catch (IOException)
		{
			Warn(string.Format(LockedFileWarningFormat, path), path);
			try
			{
				return File.ReadAllLines(path, EncodingHandler.UTF8BOMEncoding);
			}
			catch
			{
				return null;
			}
		}
	}

	public static async Task Load(string path)
	{
		var startTime = DateTime.Now;
		Debug.WriteLine($"Load: {path} : Start");

		var dir = new DirectoryInfo(path);
		if (dir.Exists)
		{
			await Task.Run(() =>
			{
				dir.EnumerateFiles("*", SearchOption.AllDirectories)
				.AsParallel()
				.Where(x => IsTargetExtension(x.Extension))
				.ForAll((childPath) =>
				{
					var key = childPath;
					var value = readAllLinesDetectEncoding(childPath.ToString());
					if (value == null)
						return;
					lock (files)
					{
						files[key.ToString()] = value;
					}
				});
			});
		}
		else
		{
			var key = path;
			var value = readAllLinesDetectEncoding(path);
			if (value == null)
				return;
			lock (files)
			{
				files[key] = value;
			}
		}

		Debug.WriteLine($"Load: {path} : End in {(DateTime.Now - startTime).TotalMilliseconds}ms");
	}

	public static async Task Load(IEnumerable<string> paths)
	{
		foreach (var path in paths)
		{
			await Load(path);
		}
	}

	public static void Clear()
	{
		files.Clear();
	}
}
