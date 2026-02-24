using System.Drawing;
using System.IO;
using System.Text;

namespace MinorShift.Emuera.Runtime.Utils.EvilMask;

internal sealed partial class Utils
{
	public static void AddColorParam(StringBuilder sb, string name, Color color)
	{
		if (color != Color.Transparent)
		{
			sb.Append(' ').Append(name).Append("='").Append(GetColorToString(color)).Append('\'');
		}
	}

	public static void AddColorParam4(StringBuilder sb, string name, Color[] colors)
	{
		if (colors != null)
		{
			sb.Append(' ').Append(name).Append("='");
			if (colors[0] == colors[1] && colors[0] == colors[2] && colors[0] == colors[3])
				sb.Append(GetColorToString(colors[0]));
			else if (colors[0] == colors[2] && colors[1] == colors[3])
				sb.Append(GetColorToString(colors[0])).Append(',').Append(GetColorToString(colors[1]));
			else if (colors[1] == colors[3])
				sb.Append(GetColorToString(colors[0])).Append(',').Append(GetColorToString(colors[1]))
					.Append(',').Append(GetColorToString(colors[2]));
			else
				for (int i = 0; i < colors.Length; i++)
				{
					sb.Append(GetColorToString(colors[i]));
					if (i + 1 < colors.Length)
						sb.Append(',');
				}
			sb.Append('\'');
		}
	}

	private static string GetColorToString(Color color)
	{
		var b = new StringBuilder();
		b.Append('#');
		int colorValue = color.R * 0x10000 + color.G * 0x100 + color.B;
		b.Append(colorValue.ToString("X6"));
		return b.ToString();
	}

	// filepathの安全性(ゲームフォルダ以外のフォルダか)を確認しない
	public static Bitmap LoadImage(string filepath)
	{
		if (!File.Exists(filepath))
			return null;
		try
		{
			var hooked = RuntimeHost.LoadImage(filepath);
			if (hooked is Bitmap bitmap)
				return bitmap;
			return new Bitmap(filepath);
		}
		catch
		{
			return null;
		}
	}

	// ビットマップファイルからアイコンファイルをつくる
	public static Icon MakeIconFromBmpFile(Bitmap bmp)
	{
		Image img = bmp;
		Bitmap bitmap = new(256, 256, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
		Graphics g = Graphics.FromImage(bitmap);
		g.DrawImage(img, new Rectangle(0, 0, 256, 256));
		g.Dispose();

		Icon icon = Icon.FromHandle(bitmap.GetHicon());
		img.Dispose();
		bitmap.Dispose();
		return icon;
	}
}
