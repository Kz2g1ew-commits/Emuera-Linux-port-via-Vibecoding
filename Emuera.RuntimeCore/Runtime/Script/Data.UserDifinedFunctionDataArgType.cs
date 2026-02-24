namespace MinorShift.Emuera.Runtime.Script.Data;

internal enum UserDifinedFunctionDataArgType
{
	Null,
	Int = 0x10,
	Str = 0x20,

	RefInt1 = 0x51,
	RefInt2 = 0x52,
	RefInt3 = 0x53,
	RefStr1 = 0x61,
	RefStr2 = 0x62,
	RefStr3 = 0x63,
	__Ref = 0x40,
	__Dimention = 0x0F,
}
