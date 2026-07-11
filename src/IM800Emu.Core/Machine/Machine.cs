using IM800Emu.Core.CPU;
using IM800Emu.Core.Device;
using IM800Emu.Core.IM800Debug;

namespace IM800Emu.Core.Machine;

public class Machine
{
	private MachineContext _context;

	public Machine(byte[] startupRom, List<Symbol> symbols)
	{
		_context = new();
		Debugger.AttachDebugger(_context);

		_context.AddSymbols(symbols);

		RAMDevice rom = new(startupRom, readOnly: true);
		RAMDevice ram = new(0x40000);

		// Address space is first decoded into 2 MiB chunks (16 MiB address space / 8)

		// First 2 MiB Chunk: BIOS
		// BIOS ROM mapped to 0x00_0000-0x03_FFFF (256 KiB)
		// BIOS extension ROMs follow in 256KB blocks until 0x1F_FFFF
		_context.MemoryBus.AddDevice(rom, 0x00_0000, 0x03_FFFF);

		// Second 2 MiB Chunk: RAM
		// RAM starts at 0x20_000, first chunk ends at 0x3F_FFFF
		_context.MemoryBus.AddDevice(ram, 0x20_0000, 0x3F_FFFF);

		ConsoleDevice consoleDevice = new(_context);

		_context.IoBus.AddDevice(consoleDevice, 0, consoleDevice.Length);

		Result resetResult = _context.Cpu.Reset();

		foreach (Result.Error error in resetResult.Errors)
		{
			Console.WriteLine(error);
		}
	}

	public Result StepFrame()
	{
		Result result = new();

		if (_context.Paused)
		{
			_context.HandlePauseState();
		}

		if (_context.CurrentFrameCyclesRemaining <= 0)
		{
			_context.CurrentFrameCyclesRemaining += _context.CyclesPerFrame;
		}

		while (_context.CurrentFrameCyclesRemaining > 0)
		{
			Result<DecodedOperation> decodeResult = _context.Cpu.Decode();
			result.Combine(decodeResult);
			int cyclesUsed = decodeResult.ResultObject.FetchCycles;

			if (decodeResult.IsSuccess)
			{
				Result<int> executeResult = _context.Cpu.Execute(decodeResult.ResultObject);
				result.Combine(executeResult);
				cyclesUsed = executeResult.ResultObject;
			}

			if (cyclesUsed == 0)
			{
				cyclesUsed = 7; // Typical instruction word fetch + execute timing
			}
			_context.CurrentFrameCyclesRemaining -= cyclesUsed;

			if (!result.IsSuccess)
			{
				foreach (var error in result.Errors)
				{
					Console.WriteLine(error);
				}

				string pcString = Debugger.GetNamedAddress(_context, decodeResult.ResultObject.BaseAddress);
				Console.WriteLine(
					$"Instruction: {decodeResult.ResultObject} at {pcString}"
				);
				Console.WriteLine($"Registers: {_context.GetStandardRegisterDisplayString()}");
				_context.Paused = true;
			}

			if (_context.LogExecution)
			{
				Console.WriteLine();
				Console.WriteLine($"Executed: {decodeResult.ResultObject}");

				Result<DecodedOperation> nextOperation = _context.Cpu.Decode();
				if (nextOperation.IsSuccess)
				{
					string pcString = Debugger.GetNamedAddress(_context, decodeResult.ResultObject.BaseAddress);
					Console.WriteLine(
						$"Next Operation: {nextOperation.ResultObject} at {pcString}"
					);
				}
				else
				{
					Console.WriteLine($"Not a valid instruction.");
				}

				Console.WriteLine(_context.GetStandardRegisterDisplayString());
				Console.WriteLine();
			}

			if (_context.Paused)
			{
				break;
			}
		}

		return result;
	}
}
