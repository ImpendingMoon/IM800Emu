using IM800Emu.Core.Device;

namespace IM800Emu.Core.Machine;

/// <summary>
///
/// </summary>
public class Machine
{
	private readonly CPU.IM800 _cpu;
	private readonly Bus.MemoryBus _memoryBus;
	private readonly Bus.MemoryBus _ioBus;
	private readonly Bus.InterruptBus _interruptBus;

	/// <summary>
	///
	/// </summary>
	/// <param name="startupRom"></param>
	public Machine(byte[] startupRom)
	{
		RAMDevice rom = new(startupRom, true);
		RAMDevice ram = new(0x40000);

		_memoryBus = new Bus.MemoryBus();
		_ioBus = new Bus.MemoryBus();
		_interruptBus = new Bus.InterruptBus();

		_memoryBus.AddDevice(rom, 0x0000_0000, rom.Length);
		_memoryBus.AddDevice(ram, 0x0020_0000, ram.Length);

		_cpu = new CPU.IM800(_memoryBus, _ioBus, _interruptBus);
		Result resetResult = _cpu.Reset();
		foreach (Result.Error error in resetResult.Errors)
		{
			Console.WriteLine(error);
		}
	}

	/// <summary>
	///
	/// </summary>
	public Result StepFrame()
	{
		Result result = new();

		Result<CPU.DecodedOperation> decodeResult = _cpu.Decode();
		result.Combine(decodeResult);

		if (decodeResult.IsSuccess)
		{
			Console.WriteLine($"Executing {decodeResult.ResultObject}");
			Result<int> executeResult = _cpu.Execute(decodeResult.ResultObject);
			result.Combine(executeResult);

			Console.WriteLine($"Took {executeResult.ResultObject} cycles");
			Console.WriteLine(_cpu.Registers.GetFullDisplayString());
			Console.WriteLine(new string('=', 80));
		}

		Thread.Sleep(500);

		return result;
	}
}
