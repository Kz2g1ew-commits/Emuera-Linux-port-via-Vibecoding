namespace MinorShift.Emuera.Runtime.Utils;

public readonly struct RuntimeRect
{
	public int X { get; }
	public int Y { get; }
	public int Width { get; }
	public int Height { get; }

	public RuntimeRect(int x, int y, int width, int height)
	{
		X = x;
		Y = y;
		Width = width;
		Height = height;
	}
}
