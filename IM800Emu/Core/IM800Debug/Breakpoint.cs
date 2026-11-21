namespace IM800Emu.Core.IM800Debug;

internal class Breakpoint
{
	public Breakpoint(uint address)
	{
		Address = address;
	}

	public uint Address { get; }
}
