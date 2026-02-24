namespace MinorShift.Emuera.Runtime.Utils;

public readonly struct RuntimePoint
{
	public int X { get; }
	public int Y { get; }

	public RuntimePoint(int x, int y)
	{
		X = x;
		Y = y;
	}
}
