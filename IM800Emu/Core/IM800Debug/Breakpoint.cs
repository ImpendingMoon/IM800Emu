namespace IM800Emu.Core.IM800Debug;

internal class Breakpoint
{
	public Breakpoint(uint address, Constants.BreakpointType type, bool active)
	{
		Address = address;
		Type = type;
		Active = active;
	}

	public uint Address { get; set; }
	public Constants.BreakpointType Type { get; set; }
	public bool Active { get; set; }

	public override string ToString()
	{
		return $"0x{Address:X8} on {Type}";
	}
}
