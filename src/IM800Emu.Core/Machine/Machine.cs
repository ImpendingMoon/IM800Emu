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
	private readonly Bus.MemoryBus _ioBus;
	private readonly Bus.InterruptBus _interruptBus;
	private readonly int _cyclesPerFrame = Constants.CpuSpeedHz / Constants.TargetFramerate;
	private int _currentFrameCyclesRemaining = 0;
	private bool _paused = false;
	private bool _singleStep = false;

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

		_cpu = new CPU.IM800(_memoryBus, _ioBus, _interruptBus, HandleBreakpointInstruction);
		Result resetResult = _cpu.Reset();
		foreach (Result.Error error in resetResult.Errors)
		{
			Console.WriteLine(error);
		}
	}

	public Result StepFrame()
	{
		Result result = new();

		if (_paused)
		{
			Console.WriteLine("Emulator Paused. Press Enter to Continue, Esc to Quit, S to step.");

			while (true)
			{
				ConsoleKeyInfo key = Console.ReadKey(true);
				if (key.Key == ConsoleKey.Enter)
				{
					_paused = false;
					_singleStep = false;
					break;
				}
				else if (key.Key == ConsoleKey.Escape)
				{
					Environment.Exit(0);
				}
				else if (key.Key == ConsoleKey.S)
				{
					_singleStep = true;
					break;
				}
			}
		}

		if (_currentFrameCyclesRemaining <= 0)
		{
			_currentFrameCyclesRemaining += _cyclesPerFrame;
		}

		while (_currentFrameCyclesRemaining > 0)
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
			_currentFrameCyclesRemaining -= cyclesUsed;

			if (!result.IsSuccess)
			{
				foreach (var error in result.Errors)
				{
					Console.WriteLine(error);
				}

				Console.WriteLine($"Instruction: {decodeResult.ResultObject} at 0x{decodeResult.ResultObject.BaseAddress}");
				Console.WriteLine($"Registers: {_cpu.Registers}");
				_paused = true;
			}

			if (_singleStep)
			{
				Console.WriteLine();
				Console.WriteLine($"Executed: {decodeResult.ResultObject}");

				Result<DecodedOperation> nextOperation = _cpu.Decode();
				if (nextOperation.IsSuccess)
				{
					Console.WriteLine($"Next Operation: {nextOperation.ResultObject} at 0x{nextOperation.ResultObject.BaseAddress:X8}");
				}
				else
				{
					Console.WriteLine($"Data at PC is not a valid instruction.");
				}

				Console.WriteLine(_cpu.Registers);
				Console.WriteLine();
			}

			if (_paused)
			{
				break;
			}
		}

		return result;
	}

	public void HandleBreakpointInstruction(uint baseAddress, uint code)
	{
		Console.WriteLine();
		Console.WriteLine($"==== Hit BKPT {code} @ 0x{baseAddress:X8} ====");

		switch (code)
		{
			case 0:
			{
				_paused = true;
				break;
			}
			case 1:
			{
				Console.WriteLine("==== REGISTER DUMP ====");
				Console.WriteLine(_cpu.Registers.GetFullDisplayString());
				Console.WriteLine("========");
				break;
			}
			case 2:
			{
				Console.WriteLine("==== STACK DUMP ====");
				Console.WriteLine(_cpu.GetStackReadout(-128, 128));
				Console.WriteLine("========");
				break;
			}
			case 3:
			{
				Console.WriteLine("==== FULL DUMP ====");
				Console.WriteLine(_cpu.Registers.GetFullDisplayString());
				Console.WriteLine(_cpu.GetStackReadout(-64, 64));
				Console.WriteLine("========");
				break;
			}
			default:
			{
				break;
			}
		}

		Console.WriteLine();
	}
}
