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
	private readonly int _cyclesPerFrame = Constants.CpuSpeedHz / Constants.TargetFramerate;
	private int _cycleRemainder = 0;

	public Machine(byte[] startupRom)
	{
		RAMDevice rom = new(startupRom, true);
		RAMDevice ram = new(0x40000);

		_memoryBus = new Bus.MemoryBus();

		// Address space is first decoded into 2 MiB chunks (16 MiB address space / 8)

		// First 2 MiB Chunk: BIOS
		// BIOS ROM mapped to 0x00_0000-0x03_FFFF (256 KiB)
		// BIOS extension ROMs follow in 256KB blocks until 0x1F_FFFF
		_memoryBus.AddDevice(rom, 0x00_0000, 0x03_FFFF);

		// Second 2 MiB Chunk: RAM
		// RAM starts at 0x20_000, first chunk ends at 0x3F_FFFF
		_memoryBus.AddDevice(ram, 0x20_0000, 0x3F_FFFF);

		ConsoleDevice consoleDevice = new();

		_ioBus = new Bus.MemoryBus();

		_ioBus.AddDevice(consoleDevice, 0, consoleDevice.Length);

		_interruptBus = new Bus.InterruptBus();

		_cpu = new CPU.IM800(_memoryBus, _ioBus, _interruptBus);
		Result resetResult = _cpu.Reset();
		foreach (Result.Error error in resetResult.Errors)
		{
			Console.WriteLine(error);
		}
	}

	public Result StepFrame()
	{
		Result result = new();

		int budget = _cyclesPerFrame + _cycleRemainder;

		while (budget > 0)
		{
			Result<CPU.DecodedOperation> decodeResult = _cpu.Decode();
			result.Combine(decodeResult);
			int cyclesUsed = decodeResult.ResultObject.FetchCycles;

			if (decodeResult.IsSuccess)
			{
				Result<int> executeResult = _cpu.Execute(decodeResult.ResultObject);
				result.Combine(executeResult);
				cyclesUsed = executeResult.ResultObject;
			}

			if (cyclesUsed == 0)
			{
				cyclesUsed = 7;
			}
			budget -= cyclesUsed;

			if (!result.IsSuccess)
			{
				foreach (var error in result.Errors)
				{
					Console.WriteLine(error);
				}

				Console.WriteLine($"Instruction: {decodeResult.ResultObject} at 0x{decodeResult.ResultObject.BaseAddress}");
				Console.WriteLine($"Registers: {_cpu.Registers}");
			}
		}

		_cycleRemainder = budget;

		return result;
	}
}
