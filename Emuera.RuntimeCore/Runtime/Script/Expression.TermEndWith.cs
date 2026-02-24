namespace MinorShift.Emuera.Runtime.Script.Statements.Expression;

internal enum TermEndWith
{
	None = 0x0000,
	EoL = 0x0001,
	Comma = 0x0002,//','終端
	RightParenthesis = 0x0004,//')'終端
	RightBracket = 0x0008,//')'終端
	Assignment = 0x0010,//')'終端


	#region EM_私家版_HTMLパラメータ拡張
	KeyWordPx = 0x0020,//'px'終端
	#endregion

	RightParenthesis_Comma = RightParenthesis | Comma,//',' or ')'
	RightBracket_Comma = RightBracket | Comma,//',' or ']'
	Comma_Assignment = Comma | Assignment,//',' or '='
	RightParenthesis_Comma_Assignment = RightParenthesis | Comma | Assignment,//',' or ')' or '='
	RightBracket_Comma_Assignment = RightBracket | Comma | Assignment,//',' or ']' or '='
}
