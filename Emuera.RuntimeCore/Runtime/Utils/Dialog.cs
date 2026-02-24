using MinorShift.Emuera.Runtime.Utils;

internal static class Dialog
{
	public static void Show(string text)
	{
		RuntimeHost.ShowInfo(text);
	}

	public static void Show(string title, string text)
	{
		RuntimeHost.ShowInfo(text, title);
	}

	public static bool ShowPrompt(string title, string text)
	{
		return RuntimeHost.ConfirmYesNo(text, title);
	}
}
