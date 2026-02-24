namespace MinorShift.Emuera.Runtime.Utils;

public readonly struct RuntimeFontSpec
{
	public string Name { get; }
	public int SizePx { get; }

	public RuntimeFontSpec(string name, int sizePx)
	{
		Name = name ?? string.Empty;
		SizePx = sizePx;
	}
}
