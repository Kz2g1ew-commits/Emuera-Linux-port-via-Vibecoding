namespace MinorShift.Emuera.Runtime.Script.Statements.Function;

internal enum ArgType
{
	Invalid = 0,

	Any = 1,
	Int = 1 << 1,
	String = 1 << 2,
	Ref = 1 << 3,
	Array = 1 << 4,
	Array1D = 1 << 5,
	Array2D = 1 << 6,
	Array3D = 1 << 7,
	Variadic = 1 << 8,
	SameAsFirst = 1 << 9,
	CharacterData = Ref | 1 << 10,
	AllowConstRef = 1 << 11,
	DisallowVoid = 1 << 12,

	RefInt = Ref | Int,
	RefAny = Ref | Any,
	RefString = Ref | String,
	RefAnyArray = RefAny | Array,
	RefIntArray = RefInt | Array,
	RefStringArray = RefString | Array,
	RefAny1D = RefAny | Array1D,
	RefInt1D = RefInt | Array1D,
	RefString1D = RefString | Array1D,
	RefAny2D = RefAny | Array2D,
	RefInt2D = RefInt | Array2D,
	RefString2D = RefString | Array2D,
	RefAny3D = RefAny | Array3D,
	RefInt3D = RefInt | Array3D,
	RefString3D = RefString | Array3D,

	VariadicAny = Variadic | Any,
	VariadicInt = Variadic | Int,
	VariadicString = Variadic | String,
	VariadicSameAsFirst = Variadic | SameAsFirst,
}
