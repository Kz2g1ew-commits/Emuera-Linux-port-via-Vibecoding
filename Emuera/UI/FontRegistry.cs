using System.Drawing.Text;

namespace MinorShift.Emuera.UI;

internal static class FontRegistry
{
	public static PrivateFontCollection Collection { get; } = new();
}
