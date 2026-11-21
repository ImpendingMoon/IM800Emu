namespace IM800Emu.Core.IM800Debug;

internal class Watchpoint
{
	public Watchpoint(uint address, bool breakOnRead, bool breakOnWrite)
	{
		Address = address;
		BreakOnRead = breakOnRead;
		BreakOnWrite = breakOnWrite;
	}

	public uint Address { get; }
	public bool BreakOnRead { get; set; }
	public bool BreakOnWrite { get; set; }
}
