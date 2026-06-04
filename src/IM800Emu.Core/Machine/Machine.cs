using IM800Emu.Core.CPU;
using IM800Emu.Core.Device;

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
		RAMDevice rom = new(startupRom, true);

		_memoryBus = new Bus.MemoryBus();

		_memoryBus.AddDevice(rom, 0x0000_0000, rom.Length);

		_cpu = new CPU.IM800(_memoryBus);
	}

	/// <summary>
	///
	/// </summary>
	public Result StepFrame()
	{
		Result result = new();

		Result<DecodedOperation> decodeResult = _cpu.Decode();
		result.Combine(decodeResult);

		if (decodeResult.IsSuccess)
		{
			Result<int> executeResult = _cpu.Execute(decodeResult.ResultObject);
			result.Combine(executeResult);
		}

		return result;
	}
}
