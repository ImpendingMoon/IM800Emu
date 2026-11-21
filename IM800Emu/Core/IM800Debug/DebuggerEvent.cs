using IM800Emu.Core.CPU;
using IM800Emu.Core.Machine;

namespace IM800Emu.Core.IM800Debug;

internal partial class Debugger
{
	// Event handlers
	private void OnBeforeMemoryRead(object? sender, MachineEvents.BeforeMemoryReadArgs args)
	{
		if (_inDebugger)
		{
			return;
		}

		if (_watchpoints.TryGetValue(args.Address, out var watchpoint))
		{
			if (watchpoint.BreakOnRead)
			{
				Console.WriteLine($"Read watchpoint hit at 0x{args.Address:X8}");
				_context.MachineAction = Constants.MachineAction.Pause;
			}
		}
	}

	private void OnBeforeMemoryWrite(object? sender, MachineEvents.BeforeMemoryWriteArgs args)
	{
		if (_inDebugger)
		{
			return;
		}

		if (_watchpoints.TryGetValue(args.Address, out var watchpoint))
		{
			if (watchpoint.BreakOnWrite)
			{
				Console.WriteLine($"Write watchpoint hit at 0x{args.Address:X8}");
				_context.MachineAction = Constants.MachineAction.Pause;
			}
		}
	}

	private void OnBeforeBadMemoryRead(object? sender, MachineEvents.BeforeBadMemoryReadArgs args)
	{
		if (_inDebugger)
		{
			return;
		}

		Console.WriteLine($"Bad read from {args.DeviceName} at relative address 0x{args.Address:X8}");
	}

	private void OnBeforeBadMemoryWrite(object? sender, MachineEvents.BeforeBadMemoryWriteArgs args)
	{
		if (_inDebugger)
		{
			return;
		}

		Console.WriteLine($"Bad write to {args.DeviceName} at relative address 0x{args.Address:X8}");
	}

	private void OnBeforeInstructionFetch(object? sender, MachineEvents.BeforeInstructionFetchArgs args)
	{
		if (_inDebugger)
		{
			return;
		}

		if (_breakpoints.TryGetValue(args.ProgramCounter, out var breakpoint))
		{
			Console.WriteLine($"Breakpoint hit at 0x{args.ProgramCounter:X8}");
			_context.MachineAction = Constants.MachineAction.Pause;
		}
	}

	private void OnInvalidInstruction(object? sender, MachineEvents.InvalidInstructionArgs args)
	{
		if (_inDebugger)
		{
			return;
		}

		Console.WriteLine($"Invalid instruction at 0x{args.DecodedOperation.BaseAddress:X8}: {args.Message}");
	}

	private void OnBkptInstruction(object? sender, MachineEvents.BkptInstructionArgs args)
	{
		Console.WriteLine();

		byte code = (byte)(args.DecodedOperation.Destination?.Data ?? 0);

		string pcString = GetNamedAddress(args.DecodedOperation.BaseAddress);

		Console.WriteLine($"==== Hit breakpoint {code} at {pcString} ====");

		switch (code)
		{
			case 0:
			{
				_context.MachineAction = Constants.MachineAction.Pause;
				break;
			}
			case 1:
			{
				Console.WriteLine("==== REGISTER + STACK DUMP ====");
				Console.WriteLine(GetFullRegisterDisplayString());
				Console.WriteLine(GetStackDump(-32, 64, 4));
				Console.WriteLine("========");
				break;
			}
			case 2:
			{
				_logExecution = true;
				break;
			}
			case 3:
			{
				_logExecution = false;
				break;
			}
		}

		Console.WriteLine();
	}

	private void OnStepFinished(object? sender, MachineEvents.StepFinishedArgs args)
	{
		if (_logExecution)
		{
			Console.WriteLine();
			Console.WriteLine($"Executed: {_context.CurrentOperation}");

			bool previousInDebugState = _inDebugger;
			_inDebugger = true; // Don't error here

			DecodedOperation nextOperation = _context.Cpu.Decode();

			string pcString = GetNamedAddress(nextOperation.BaseAddress);

			Console.WriteLine($"Next Operation: {nextOperation} at {pcString}");
			PrintSourceContext(nextOperation.BaseAddress);

			Console.WriteLine(GetStandardRegisterDisplayString());
			Console.WriteLine();

			_inDebugger = previousInDebugState;
		}
	}

	private void OnFrameFinished(object? sender, MachineEvents.FrameFinishedArgs args)
	{
		if (_inDebugger)
		{
			return;
		}

		// TODO (maybe a step frame command?)
	}

}
