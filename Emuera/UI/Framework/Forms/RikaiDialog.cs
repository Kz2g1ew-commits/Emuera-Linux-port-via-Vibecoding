using MinorShift.Emuera.Runtime.Config;
using MinorShift.Emuera.UI.Game;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;

namespace MinorShift.Emuera.Forms;

public partial class RikaiDialog : Form
{
	private readonly byte[] mEdict;
	private byte[] mEdictIndex;
	public delegate void RikaiSendIndex(byte[] edictind);
	private readonly RikaiSendIndex rikaiSendIndex;
	private readonly List<string> dialogLines = new(6);
	private readonly DateTime start = DateTime.Now;

	public RikaiDialog(byte[] edict, RikaiSendIndex rikaiSendIndex)
	{
		mEdict = edict;
		this.rikaiSendIndex = rikaiSendIndex;

		for (int i = 0; i < 6; i++)
			dialogLines.Add(string.Empty);

		InitializeComponent();

		backgroundWorker.DoWork += bwDoWork;
		backgroundWorker.RunWorkerCompleted += bwRunWorkerCompleted;
		backgroundWorker.ProgressChanged += bwProgressChanged;

		label.Text = "starting";
		backgroundWorker.RunWorkerAsync();
	}

	private void bwRunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
	{
		if (e.Error != null)
		{
			MessageBox.Show($"Failed to generate {Config.RikaiFilename}.ind.\n{e.Error.Message}", "Rikaichan");
			return;
		}

		if (mEdictIndex == null || mEdictIndex.Length == 0)
		{
			MessageBox.Show($"Failed to generate {Config.RikaiFilename}.ind.", "Rikaichan");
			return;
		}

		rikaiSendIndex(mEdictIndex);
	}

	private void bwProgressChanged(object sender, ProgressChangedEventArgs e)
	{
		var per = e.ProgressPercentage;
		dialogLines[0] = $"Generating {Config.RikaiFilename}.ind, {per}% done.";
		if (per >= 20 && dialogLines[1].Length == 0)
		{
			var timeSpan = DateTime.Now - start;
			dialogLines[1] = $"20% in {(int)timeSpan.TotalSeconds} seconds";
		}
		if (per >= 40 && dialogLines[2].Length == 0)
		{
			var timeSpan = DateTime.Now - start;
			dialogLines[2] = $"40% in {(int)timeSpan.TotalSeconds} seconds";
		}
		if (per >= 60 && dialogLines[3].Length == 0)
		{
			var timeSpan = DateTime.Now - start;
			dialogLines[3] = $"60% in {(int)timeSpan.TotalSeconds} seconds";
		}
		if (per >= 80 && dialogLines[4].Length == 0)
		{
			var timeSpan = DateTime.Now - start;
			dialogLines[4] = $"80% in {(int)timeSpan.TotalSeconds} seconds";
		}
		if (per >= 100 && dialogLines[5].Length == 0)
		{
			var timeSpan = DateTime.Now - start;
			dialogLines[5] = $"100% in {(int)timeSpan.TotalSeconds} seconds";
		}
		label.Text = string.Join("\n", dialogLines);
	}

	private void bwDoWork(object sender, DoWorkEventArgs e)
	{
		var worker = sender as BackgroundWorker;
		var outputPath = Path.Combine(Program.ExeDir, Config.RikaiFilename + ".ind");

		if (!RikaiIndexGenerator.TryGenerateAndSave(
			mEdict,
			outputPath,
			progress => worker?.ReportProgress(progress),
			out mEdictIndex,
			out var errorMessage))
		{
			throw new IOException(errorMessage);
		}
	}
}
