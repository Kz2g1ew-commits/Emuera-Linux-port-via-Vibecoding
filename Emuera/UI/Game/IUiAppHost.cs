namespace MinorShift.Emuera.UI.Game;

internal interface IUiAppHost
{
	string Name { get; }
	bool TryRun(string[] args, object appIcon);
}
