namespace MinorShift.Emuera.UI.Game;

internal sealed class UnsupportedUiAppHost : IUiAppHost
{
	public UnsupportedUiAppHost(string name, string reason)
	{
		Name = name ?? "unsupported";
		Reason = reason ?? string.Empty;
	}

	public string Name { get; }
	public string Reason { get; }

	public bool TryRun(string[] args, object appIcon)
	{
		return false;
	}
}
