using System;

namespace MinorShift.Emuera.Runtime.Script;

[Flags]
public enum RuntimeFontStyleFlags
{
	Regular = 0,
	Bold = 1,
	Italic = 2,
	Strikeout = 4,
	Underline = 8,
}
