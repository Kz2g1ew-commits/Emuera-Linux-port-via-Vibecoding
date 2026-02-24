using System;
using System.IO;
using System.Text;

namespace MinorShift.Emuera.UI.Game;

internal sealed class TextDebugDialogHandle : IUiDebugDialogHandle
{
	private readonly IDebugDialogConsoleHost console;
	private readonly string backendName;
	private bool created;
	private string snapshotPath;

	public TextDebugDialogHandle(IDebugDialogConsoleHost console, string backendName)
	{
		this.console = console ?? throw new ArgumentNullException(nameof(console));
		this.backendName = string.IsNullOrWhiteSpace(backendName) ? "text-debug" : backendName;
	}

	public bool IsCreated => created;

	public void Focus()
	{
		if (!created)
		{
			Show();
			return;
		}

		Console.Error.WriteLine($"[emuera:{backendName}] debug snapshot: {GetSnapshotPath()}");
	}

	public void Show()
	{
		created = true;
		Console.Error.WriteLine($"[emuera:{backendName}] debug dialog fallback enabled.");
		UpdateData();
	}

	public void UpdateData()
	{
		if (!created)
			return;

		var path = GetSnapshotPath();
		try
		{
			var dir = Path.GetDirectoryName(path);
			if (!string.IsNullOrWhiteSpace(dir))
				Directory.CreateDirectory(dir);
			File.WriteAllText(path, BuildSnapshotText(), Encoding.UTF8);
			Console.Error.WriteLine($"[emuera:{backendName}] debug snapshot updated: {path}");
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"[emuera:{backendName}] debug snapshot write failed: {ex.Message}");
		}
	}

	public void Close()
	{
		created = false;
	}

	public void Dispose()
	{
		created = false;
	}

	private string GetSnapshotPath()
	{
		if (!string.IsNullOrWhiteSpace(snapshotPath))
			return snapshotPath;

		string baseDir = string.IsNullOrWhiteSpace(Program.DebugDir) ? AppContext.BaseDirectory : Program.DebugDir;
		snapshotPath = Path.Combine(baseDir, "debug-dialog.snapshot.txt");
		return snapshotPath;
	}

	private string BuildSnapshotText()
	{
		var now = DateTime.Now;
		var builder = new StringBuilder(4096);
		builder.AppendLine($"# Emuera Debug Snapshot ({backendName})");
		builder.AppendLine($"Generated: {now:yyyy-MM-dd HH:mm:ss}");
		builder.AppendLine();
		builder.AppendLine("## Trace");
		builder.AppendLine(console.GetDebugTraceLog(force: true) ?? string.Empty);
		builder.AppendLine();
		builder.AppendLine("## Console");
		builder.AppendLine(console.DebugConsoleLog ?? string.Empty);
		builder.AppendLine();
		builder.AppendLine($"RunERBFromMemory: {console.RunERBFromMemory}");
		builder.AppendLine($"IsInProcess: {console.IsInProcess}");
		return builder.ToString();
	}
}
