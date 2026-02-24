using System.Runtime.InteropServices;

namespace MinorShift.Emuera.Runtime.Utils;

public static class WinInput
{
	[DllImport("user32.dll")]
	private static extern short GetKeyStateWindows(int nVirtKey);

	public static short GetKeyState(int nVirtKey)
	{
		if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			return 0;

		return GetKeyStateWindows(nVirtKey);
	}
}
