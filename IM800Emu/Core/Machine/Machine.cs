using IM800Emu.Core.CPU;
using IM800Emu.Core.Device;
using IM800Emu.Core.IM800Debug;

namespace IM800Emu.Core.Machine;

public class Machine
{
	private readonly MachineContext _context;

	public Machine(byte[] startupRom, List<Symbol> symbols)
	{
		_context = new MachineContext();
		Debugger.AttachDebugger(_context);

		_context.AddSymbols(symbols);

		RAMDevice biosRom = new(startupRom, true);
		RAMDevice biosRam = new(0x1000);
		RAMDevice systemRam = new(0x40000);


		// Address space is first decoded into 2 MiB chunks (16 MiB address space / 8)

		// BIOS address space mapped to 0x00_0000-0x03_FFFF (256 KiB) for ROM and firmware RAM
		// BIOS extension ROMs follow in 256KB blocks until 0x1F_FFFF
		_context.MemoryBus.AddDevice(biosRom, Constants.MemoryBaseWaitStates, 0x00_0000, 0xFFFF);
		_context.MemoryBus.AddDevice(biosRam, Constants.MemoryBaseWaitStates, 0x01_0000, 0x1000);

		// Second 2 MiB Chunk: System RAM
		// RAM starts at 0x20_000, first chunk ends at 0x3F_FFFF
		_context.MemoryBus.AddDevice(systemRam, Constants.MemoryBaseWaitStates, 0x20_0000, 0x3F_FFFF);

		// TEMP test fake cards
		_context.MemoryBus.AddDevice(new RAMDevice(
				[
					// +0 Magic
					0x45, 0x58, 0x50, 0x44,
					// +4 Checksum
					0x0D, 0x1F, 0xDF, 0x2B,
					// +8 ROM Length
					0x42, 0x00, 0x00, 0x00,
					// +12 API Version
					0x01, 0x00,
					// +14 Vendor ID
					0x00, 0x00, 0x00, 0x00,
					// +18 Device ID
					0x00, 0x00,
					// +20 Init Routine Offset
					0x40, 0x00, 0x00, 0x00,
					// +24 Shutdown Routine Offset
					0x00, 0x00, 0x00, 0x00,
					// +28 Reserved (36 bytes)
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
					0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
					0x00, 0x00, 0x00, 0x00,
					// +40 (Configured Init Routine Offset)
					0x8A, 0x1E, // RET
				], true),
			Constants.MemoryBaseWaitStates,
			0x080000, 0x040000
		);


		ConsoleDevice consoleDevice = new(_context);

		_context.IoBus.AddDevice(consoleDevice, Constants.IOBaseWaitStates, 0, consoleDevice.Length);

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
			Result instructionResult = new();

			Result<DecodedOperation> decodeResult = _context.Cpu.Decode();
			instructionResult.Combine(decodeResult);

			_context.CurrentOperation = decodeResult.ResultObject;
			int cyclesUsed = decodeResult.ResultObject.FetchCycles;

			if (decodeResult.IsSuccess)
			{
				Result<int> executeResult = _context.Cpu.Execute(decodeResult.ResultObject);
				instructionResult.Combine(executeResult);
				cyclesUsed = executeResult.ResultObject;
			}

			if (cyclesUsed == 0)
			{
				cyclesUsed = 7; // Typical instruction word fetch + execute timing
			}

			_context.CurrentFrameCyclesRemaining -= cyclesUsed;

			//if (!instructionResult.IsSuccess)
			//{
			//Console.WriteLine();

			//foreach (Result.Error error in instructionResult.Errors)
			//{
			//	Console.WriteLine(error);
			//}

			//string pcString = Debugger.GetNamedAddress(_context, decodeResult.ResultObject.BaseAddress);
			//Console.WriteLine(
			//	$"Instruction: {decodeResult.ResultObject} at {pcString}"
			//);
			//Console.WriteLine($"Registers: {_context.GetStandardRegisterDisplayString()}");
			// _context.Paused = true;
			//}

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
					Console.WriteLine("Not a valid instruction.");
				}

				Console.WriteLine(_context.GetStandardRegisterDisplayString());
				Console.WriteLine();
			}

			if (_context.Paused)
			{
				break;
			}

			result.Combine(instructionResult);
		}

		return result;
	}
}
