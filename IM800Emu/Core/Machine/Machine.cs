using IM800Emu.Core.CPU;
using IM800Emu.Core.Device;
using IM800Emu.Core.IM800Debug;
using Raylib_cs;

namespace IM800Emu.Core.Machine;

public class Machine
{
	private readonly MachineContext _context;
	private readonly VideoDevice _videoDevice;

	public Machine(byte[] startupRom, List<Symbol> symbols)
	{
		_context = new MachineContext();
		LegacyDebugger.AttachDebugger(_context);

		_context.AddSymbols(symbols);

		// Memory Map:
		// 0x000000-0x1FFFFF: ROM (whatever the program has)
		// 0x200000-0x3FFFFF: RAM (512k)
		// 0x400000-0x5FFFFF: VRAM (64k)
		RAMDevice romDevice = new(startupRom, true, "Program ROM", _context);
		RAMDevice ramDevice = new(Config.CpuMemorySizeBytes, "RAM", _context);
		RAMDevice vramDevice = new(64 * 1024, "VRAM", _context);

		_context.MemoryBus.AddDevice(romDevice, Config.MemoryBaseWaitStates, 0x000000, 0x200000);
		_context.MemoryBus.AddDevice(ramDevice, Config.MemoryBaseWaitStates, 0x200000, 0x200000);
		_context.MemoryBus.AddDevice(vramDevice, Config.MemoryBaseWaitStates, 0x400000, 0x200000);

		// IO Map:
		// 0x00-0x03: UART
		// 0x04-0x08: Controller
		ConsoleDevice uart = new("UART", _context);
		ControllerDevice controller = new("Controller", _context);
		_videoDevice = new VideoDevice(vramDevice, "VDP", _context);

		_context.IoBus.AddDevice(uart, Config.IOBaseWaitStates, 0, 4);
		_context.IoBus.AddDevice(controller, Config.IOBaseWaitStates, 4, 4);
		_context.IoBus.AddDevice(_videoDevice, Config.IOBaseWaitStates, 8, 4);

		_context.InterruptBus.AddDevice(_videoDevice, 1);

		Result resetResult = _context.Cpu.Reset();

		foreach (Result.Error error in resetResult.Errors)
		{
			Console.WriteLine(error);
		}
	}

	public Image GetFrame()
	{
		return _videoDevice.GetFrame();
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
			Result instructionResult = StepInstruction();
			result.Combine(instructionResult);

			if (_context.Paused)
			{
				break;
			}
		}

		_context.Events.RaiseFrameFinished();

		return result;
	}

	private Result StepInstruction()
	{
		Result result = new();

		uint pc = _context.Cpu.Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		_context.Events.RaiseBeforeInstructionFetch(pc);
		if (_context.CancelOperation)
		{
			return result;
		}

		Result<DecodedOperation> decodeResult = _context.Cpu.Decode();
		result.Combine(decodeResult);

		_context.CurrentOperation = decodeResult.ResultObject;

		int cyclesUsed = decodeResult.ResultObject.FetchCycles;

		if (decodeResult.IsSuccess)
		{
			Result<int> executeResult = _context.Cpu.Execute(decodeResult.ResultObject);
			result.Combine(executeResult);

			cyclesUsed = executeResult.ResultObject;
		}

		if (cyclesUsed == 0)
		{
			cyclesUsed = 4;
		}

		_context.CurrentFrameCyclesRemaining -= cyclesUsed;

		if (!result.IsSuccess)
		{
			LogInstructionError(decodeResult);
			_context.Paused = true;

			return result;
		}

		if (_context.LogExecution)
		{
			LogInstruction(decodeResult.ResultObject);
		}

		_context.Events.RaiseStepFinished();

		return result;
	}

	private void LogInstructionError(Result<DecodedOperation> decodeResult)
	{
		Console.WriteLine();

		foreach (Result.Error error in decodeResult.Errors)
		{
			Console.WriteLine(error);
		}

		string pcString = LegacyDebugger.GetNamedAddress(_context, decodeResult.ResultObject.BaseAddress);

		Console.WriteLine($"Instruction: {decodeResult.ResultObject} at {pcString}");

		Console.WriteLine($"Registers: {_context.GetStandardRegisterDisplayString()}");
	}

	private void LogInstruction(DecodedOperation operation)
	{
		Console.WriteLine();
		Console.WriteLine($"Executed: {operation}");

		Result<DecodedOperation> nextOperation = _context.Cpu.Decode();

		if (!nextOperation.IsSuccess)
		{
			Console.WriteLine("Not a valid instruction.");
		}
		else
		{
			string pcString = LegacyDebugger.GetNamedAddress(_context, nextOperation.ResultObject.BaseAddress);

			Console.WriteLine($"Next Operation: {nextOperation.ResultObject} at {pcString}");
		}

		Console.WriteLine(_context.GetStandardRegisterDisplayString());
		Console.WriteLine();
	}
}
