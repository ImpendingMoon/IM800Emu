using IM800Emu.Core.CPU;

namespace IM800Emu.Core.Bus;

/// <summary>
/// 
/// </summary>
public class MemoryBus
{
	public MemoryResult Read(uint address, Constants.DataSize size)
	{
		return new();
	}

	public MemoryResult Write(uint address, Constants.DataSize size, uint value)
	{
		return new();
	}
}