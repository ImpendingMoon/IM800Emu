using IM800Emu.Core.CPU;

namespace IM800Emu.Core.Machine;

/// <summary>
///
/// </summary>
public class Machine
{
	private readonly CPU.IM800 _cpu;
	private readonly Bus.MemoryBus _memoryBus;

	/// <summary>
	///
	/// </summary>
	/// <param name="startupRom"></param>
	public Machine(byte[] startupRom)
	{
		_memoryBus = new Bus.MemoryBus();
		_cpu = new CPU.IM800(_memoryBus);
	}

	/// <summary>
	///
	/// </summary>
	public void StepFrame()
	{
		Result<DecodedOperation> operation = _cpu.Decode();

		if (operation.IsSuccess)
		{
			_ = _cpu.Execute(operation.ResultObject);
		}
	}
}
