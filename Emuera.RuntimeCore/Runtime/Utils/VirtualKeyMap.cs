using System;
using System.Collections.Generic;

namespace MinorShift.Emuera.Runtime.Utils;

public static class VirtualKeyMap
{
	private const int ModifierShiftMask = 1 << 16;
	private const int ModifierControlMask = 1 << 17;
	private const int ModifierAltMask = 1 << 18;

	private static readonly Dictionary<string, int> AliasMap = new(StringComparer.OrdinalIgnoreCase)
	{
		["LBUTTON"] = 0x01,
		["RBUTTON"] = 0x02,
		["MBUTTON"] = 0x04,
		["XBUTTON1"] = 0x05,
		["XBUTTON2"] = 0x06,
		["BACK"] = 0x08,
		["BACKSPACE"] = 0x08,
		["TAB"] = 0x09,
		["CLEAR"] = 0x0C,
		["RETURN"] = 0x0D,
		["ENTER"] = 0x0D,
		["SHIFT"] = 0x10,
		["SHIFTKEY"] = 0x10,
		["CONTROL"] = 0x11,
		["CONTROLKEY"] = 0x11,
		["CTRL"] = 0x11,
		["MENU"] = 0x12,
		["ALT"] = 0x12,
		["PAUSE"] = 0x13,
		["CAPITAL"] = 0x14,
		["CAPSLOCK"] = 0x14,
		["KANA"] = 0x15,
		["JUNJA"] = 0x17,
		["FINAL"] = 0x18,
		["KANJI"] = 0x19,
		["ESCAPE"] = 0x1B,
		["ESC"] = 0x1B,
		["SPACE"] = 0x20,
		["SPACEBAR"] = 0x20,
		["PRIOR"] = 0x21,
		["PAGEUP"] = 0x21,
		["PGUP"] = 0x21,
		["NEXT"] = 0x22,
		["PAGEDOWN"] = 0x22,
		["PGDN"] = 0x22,
		["END"] = 0x23,
		["HOME"] = 0x24,
		["LEFT"] = 0x25,
		["UP"] = 0x26,
		["RIGHT"] = 0x27,
		["DOWN"] = 0x28,
		["SELECT"] = 0x29,
		["PRINT"] = 0x2A,
		["EXECUTE"] = 0x2B,
		["SNAPSHOT"] = 0x2C,
		["PRINTSCREEN"] = 0x2C,
		["INSERT"] = 0x2D,
		["DELETE"] = 0x2E,
		["HELP"] = 0x2F,
		["LWIN"] = 0x5B,
		["RWIN"] = 0x5C,
		["APPS"] = 0x5D,
		["SLEEP"] = 0x5F,
		["NUMLOCK"] = 0x90,
		["SCROLL"] = 0x91,
		["SCROLLLOCK"] = 0x91,
		["LSHIFTKEY"] = 0xA0,
		["RSHIFTKEY"] = 0xA1,
		["LCONTROLKEY"] = 0xA2,
		["RCONTROLKEY"] = 0xA3,
		["LMENU"] = 0xA4,
		["RMENU"] = 0xA5,
		["OEM1"] = 0xBA,
		["OEMPLUS"] = 0xBB,
		["OEMCOMMA"] = 0xBC,
		["OEMMINUS"] = 0xBD,
		["OEMPERIOD"] = 0xBE,
		["OEM2"] = 0xBF,
		["OEM3"] = 0xC0,
		["OEM4"] = 0xDB,
		["OEM5"] = 0xDC,
		["OEM6"] = 0xDD,
		["OEM7"] = 0xDE,
		["OEM8"] = 0xDF,
		["OEM102"] = 0xE2,
	};

	public static bool TryParseKeyName(string keyName, out int keyCode)
	{
		keyCode = 0;
		if (string.IsNullOrWhiteSpace(keyName))
			return false;

		var trimmed = keyName.Trim();
		if (int.TryParse(trimmed, out var numeric))
		{
			if (numeric < 0 || numeric > ushort.MaxValue)
				return false;
			keyCode = numeric;
			return true;
		}

		if (AliasMap.TryGetValue(trimmed, out var mapped))
		{
			keyCode = mapped;
			return true;
		}

		if (trimmed.Length == 1)
		{
			var ch = trimmed[0];
			if (char.IsLetter(ch))
			{
				keyCode = char.ToUpperInvariant(ch);
				return true;
			}

			if (char.IsDigit(ch))
			{
				keyCode = ch;
				return true;
			}
		}

		if (trimmed.Length >= 2 && (trimmed[0] == 'F' || trimmed[0] == 'f') && int.TryParse(trimmed[1..], out var fKeyNo))
		{
			if (fKeyNo >= 1 && fKeyNo <= 24)
			{
				keyCode = 0x70 + fKeyNo - 1;
				return true;
			}
		}

		if (Enum.TryParse(trimmed, ignoreCase: true, out ConsoleKey parsedKey) && TryMapConsoleKey(parsedKey, out var consoleKeyCode))
		{
			keyCode = consoleKeyCode;
			return true;
		}

		return false;
	}

	public static bool TryMapConsoleKey(ConsoleKey key, out int keyCode)
	{
		if (key >= ConsoleKey.A && key <= ConsoleKey.Z)
		{
			keyCode = (int)key;
			return true;
		}

		if (key >= ConsoleKey.D0 && key <= ConsoleKey.D9)
		{
			keyCode = 0x30 + ((int)key - (int)ConsoleKey.D0);
			return true;
		}

		if (key >= ConsoleKey.NumPad0 && key <= ConsoleKey.NumPad9)
		{
			keyCode = 0x60 + ((int)key - (int)ConsoleKey.NumPad0);
			return true;
		}

		if (key >= ConsoleKey.F1 && key <= ConsoleKey.F24)
		{
			keyCode = 0x70 + ((int)key - (int)ConsoleKey.F1);
			return true;
		}

		keyCode = key switch
		{
			ConsoleKey.Backspace => 0x08,
			ConsoleKey.Tab => 0x09,
			ConsoleKey.Clear => 0x0C,
			ConsoleKey.Enter => 0x0D,
			ConsoleKey.Pause => 0x13,
			ConsoleKey.Escape => 0x1B,
			ConsoleKey.Spacebar => 0x20,
			ConsoleKey.PageUp => 0x21,
			ConsoleKey.PageDown => 0x22,
			ConsoleKey.End => 0x23,
			ConsoleKey.Home => 0x24,
			ConsoleKey.LeftArrow => 0x25,
			ConsoleKey.UpArrow => 0x26,
			ConsoleKey.RightArrow => 0x27,
			ConsoleKey.DownArrow => 0x28,
			ConsoleKey.Select => 0x29,
			ConsoleKey.Print => 0x2A,
			ConsoleKey.Execute => 0x2B,
			ConsoleKey.PrintScreen => 0x2C,
			ConsoleKey.Insert => 0x2D,
			ConsoleKey.Delete => 0x2E,
			ConsoleKey.Help => 0x2F,
			ConsoleKey.LeftWindows => 0x5B,
			ConsoleKey.RightWindows => 0x5C,
			ConsoleKey.Applications => 0x5D,
			ConsoleKey.Sleep => 0x5F,
			ConsoleKey.Multiply => 0x6A,
			ConsoleKey.Add => 0x6B,
			ConsoleKey.Separator => 0x6C,
			ConsoleKey.Subtract => 0x6D,
			ConsoleKey.Decimal => 0x6E,
			ConsoleKey.Divide => 0x6F,
			ConsoleKey.Oem1 => 0xBA,
			ConsoleKey.OemPlus => 0xBB,
			ConsoleKey.OemComma => 0xBC,
			ConsoleKey.OemMinus => 0xBD,
			ConsoleKey.OemPeriod => 0xBE,
			ConsoleKey.Oem2 => 0xBF,
			ConsoleKey.Oem3 => 0xC0,
			ConsoleKey.Oem4 => 0xDB,
			ConsoleKey.Oem5 => 0xDC,
			ConsoleKey.Oem6 => 0xDD,
			ConsoleKey.Oem7 => 0xDE,
			ConsoleKey.Oem8 => 0xDF,
			ConsoleKey.Oem102 => 0xE2,
			ConsoleKey.Process => 0xE5,
			ConsoleKey.Packet => 0xE7,
			ConsoleKey.Attention => 0xF6,
			ConsoleKey.CrSel => 0xF7,
			ConsoleKey.ExSel => 0xF8,
			ConsoleKey.EraseEndOfFile => 0xF9,
			ConsoleKey.Play => 0xFA,
			ConsoleKey.Zoom => 0xFB,
			ConsoleKey.NoName => 0xFC,
			ConsoleKey.Pa1 => 0xFD,
			ConsoleKey.OemClear => 0xFE,
			_ => 0,
		};

		return keyCode != 0;
	}

	public static int ComposeKeyData(int keyCode, ConsoleModifiers modifiers)
	{
		var data = keyCode & ushort.MaxValue;
		if ((modifiers & ConsoleModifiers.Shift) != 0)
			data |= ModifierShiftMask;
		if ((modifiers & ConsoleModifiers.Control) != 0)
			data |= ModifierControlMask;
		if ((modifiers & ConsoleModifiers.Alt) != 0)
			data |= ModifierAltMask;
		return data;
	}
}
