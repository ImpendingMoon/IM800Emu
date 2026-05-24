using IM800Emu.Core.CPU;

namespace IM800Emu.Core.Bus;

/// <summary>
/// 
/// </summary>
public class MemoryBus
{
	public Result<MemoryOperation> Read(uint address, Constants.DataSize size)
	{
		return new();
	}

	public Result<MemoryOperation> Write(uint address, Constants.DataSize size, uint value)
	{
		return new();
	}
}
