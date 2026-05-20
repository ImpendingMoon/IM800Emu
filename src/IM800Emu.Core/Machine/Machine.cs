namespace IM800Emu.Core.Machine;

/// <summary>
/// 
/// </summary>
public class Machine
{
	private CPU.IM800 _cpu;
	private Bus.MemoryBus _memoryBus;

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
		var operation = _cpu.Decode();
		_cpu.Execute(operation);
	}
}
