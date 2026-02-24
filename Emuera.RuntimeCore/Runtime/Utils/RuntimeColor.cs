namespace MinorShift.Emuera.Runtime.Utils;

public readonly struct RuntimeColor
{
	public byte R { get; }
	public byte G { get; }
	public byte B { get; }

	public RuntimeColor(byte r, byte g, byte b)
	{
		R = r;
		G = g;
		B = b;
	}

	public int ToRgb24()
	{
		return (R << 16) | (G << 8) | B;
	}

	public static RuntimeColor FromRgb24(int rgb)
	{
		return new RuntimeColor(
			(byte)((rgb >> 16) & 0xFF),
			(byte)((rgb >> 8) & 0xFF),
			(byte)(rgb & 0xFF));
	}
}
