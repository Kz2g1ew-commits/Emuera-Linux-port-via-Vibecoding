using System.Drawing;
using System.Windows.Forms;
using MinorShift.Emuera.UI.Game;

namespace MinorShift.Emuera.Forms;

internal sealed class WinFormsAppUiHost : IUiAppHost
{
	public string Name => "winforms";

	public bool TryRun(string[] args, object appIcon)
	{
		Application.SetCompatibleTextRenderingDefault(false);
		ApplicationConfiguration.Initialize();
		using var window = new MainWindow(args);
		window.TranslateUI();
		if (appIcon is Icon icon)
			window.SetupIcon(icon);
		Application.Run(window);
		return true;
	}
}
