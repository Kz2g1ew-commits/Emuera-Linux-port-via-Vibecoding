using System.IO;
using System;

namespace MinorShift.Emuera.Runtime.Utils;

public static class AssemblyData
{
	static AssemblyData()
	{
		ExePath = Environment.ProcessPath;
		#region eee_カレントディレクトリー
		WorkingDir = EnsureTrailingSeparator(Directory.GetCurrentDirectory());
		#endregion
		ExeDir = EnsureTrailingSeparator(Path.GetDirectoryName(ExePath) ?? AppContext.BaseDirectory);
		ExeName = Path.GetFileName(ExePath);
		emueraVer = typeof(AssemblyData).Assembly.GetName().Version;

		EmueraVersionText = "Emuera.NET " + RuntimeHost.GetProductVersion();
	}

	/// <summary>
	/// 実行ファイルのパス
	/// </summary>
	public static readonly string ExePath;

	public readonly static Version emueraVer;

	public readonly static string EmueraVersionText;

	/// <summary>
	/// 実行ファイルのディレクトリ。最後に\を付けたstring
	/// </summary>
	public static readonly string ExeDir;

	#region eee_カレントディレクトリー
	/// <summary>
	/// 実行ファイルのディレクトリ。最後に\を付けたstring
	/// </summary>
	public static readonly string WorkingDir;
	#endregion

	/// <summary>
	/// 実行ファイルの名前。ディレクトリなし
	/// </summary>
	public static readonly string ExeName;

	private static string EnsureTrailingSeparator(string path)
	{
		var fullPath = Path.GetFullPath(path);
		if (!fullPath.EndsWith(Path.DirectorySeparatorChar))
			return fullPath + Path.DirectorySeparatorChar;
		return fullPath;
	}

	/// <summary>
	/// 2重起動防止。既に同名exeが実行されているならばtrueを返す
	/// </summary>
	/// <returns></returns>
	public static bool PrevInstance()
	{
		string thisProcessName = System.Diagnostics.Process.GetCurrentProcess().ProcessName;
		if (System.Diagnostics.Process.GetProcessesByName(thisProcessName).Length > 1)
		{
			return true;
		}
		return false;

	}
}
