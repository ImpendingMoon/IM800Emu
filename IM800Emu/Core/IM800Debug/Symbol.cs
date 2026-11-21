namespace IM800Emu.Core.IM800Debug;

internal class Symbol
{
	public Symbol(Constants.SymbolType type, string name, long value)
	{
		Type = type;
		Name = name;
		Value = value;
	}

	public Constants.SymbolType Type { get; set; }
	public string Name { get; set; }
	public long Value { get; set; }
}
