namespace IM800Emu.Core.IM800Debug;

internal partial class Debugger
{
	// Command handlers
	private bool HandleHelp(string[] args)
	{
		Console.WriteLine("Available commands:");
		Console.WriteLine("\tcontinue, c              Resume execution");
		Console.WriteLine("\tstep, s                  Single-step one instruction");
		Console.WriteLine("\tquit, q, exit            Quit the emulator");
		Console.WriteLine("\tlog <on|off>             Toggle execution logging");
		Console.WriteLine("\tregs, registers, r       Dump CPU registers");
		Console.WriteLine("\tstack, st [minus] [plus] Dump stack memory relative to SP (default -32 64)");
		Console.WriteLine("\tmem, m <addr> [length]   Dump a region of memory (default length 64)");
		Console.WriteLine("\tbreak, b <list|set|clear> <address>");
		Console.WriteLine("\twatch, w <list|set|clear> <r|w|rw> <address>");
		Console.WriteLine("\thelp, ?                  Show this help text");
		return false;
	}

	private bool HandleContinue(string[] args)
	{
		_context.MachineAction = Constants.MachineAction.Continue;
		_logExecution = false;
		_inDebugger = false;
		return true; // Exit debugger
	}

	private bool HandleStep(string[] args)
	{
		_logExecution = true;
		return true;
	}

	private bool HandleQuit(string[] args)
	{
		Environment.Exit(0);
		return true;
	}

	private bool HandleLog(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine($"Execution logging is currently {(_logExecution ? "ON" : "OFF")}.");
			return false;
		}

		switch (args[0].ToUpperInvariant())
		{
			case "ON":
			{
				_logExecution = true;
				Console.WriteLine("Execution logging enabled.");
				break;
			}
			case "OFF":
			{
				_logExecution = false;
				Console.WriteLine("Execution logging disabled.");
				break;
			}
			default:
			{
				Console.WriteLine("Usage: log <on|off>");
				break;
			}
		}

		return false;
	}

	private bool HandleDumpRegisters(string[] args)
	{
		Console.WriteLine("==== REGISTER DUMP ====");
		Console.WriteLine(GetFullRegisterDisplayString());
		Console.WriteLine("========");
		return false;
	}

	private bool HandleDumpStack(string[] args)
	{
		int minus = -32;
		int plus = 64;

		if (args.Length >= 1 && !TryParseNumber(args[0], out minus))
		{
			Console.WriteLine($"Invalid value for 'minus': {args[0]}");
			return false;
		}

		if (args.Length >= 2 && !TryParseNumber(args[1], out plus))
		{
			Console.WriteLine($"Invalid value for 'plus': {args[1]}");
			return false;
		}

		Console.WriteLine("==== STACK DUMP ====");
		Console.WriteLine(GetStackDump(minus, plus, 4));
		Console.WriteLine("========");

		return false;
	}

	private bool HandleDumpMemory(string[] args)
	{
		if (args.Length < 1)
		{
			Console.WriteLine("Usage: mem <address> [length]");
			return false;
		}

		if (!TryParseAddress(args[0], out uint address))
		{
			Console.WriteLine($"Invalid address: {args[0]}");
			return false;
		}

		int length = 64;
		if (args.Length >= 2 && !TryParseNumber(args[1], out length))
		{
			Console.WriteLine($"Invalid length: {args[1]}");
			return false;
		}

		if (length <= 0)
		{
			Console.WriteLine("Length must be positive.");
			return false;
		}

		Console.WriteLine("==== MEMORY DUMP ====");
		Console.WriteLine(GetMemoryDump(address, length, 16));
		Console.WriteLine("========");

		return false;
	}

	private bool HandleBreak(string[] args)
	{
		if (args.Length < 1)
		{
			Console.WriteLine("Usage: break <list|set|clear>");
			return false;
		}

		if (string.Equals(args[0], "list", StringComparison.OrdinalIgnoreCase))
		{
			foreach (var breakpoint in _breakpoints)
			{
				if (breakpoint.Type == Constants.BreakpointType.Execute)
				{
					Console.WriteLine(breakpoint);
				}
			}
			return false;
		}

		if (string.Equals(args[0], "set", StringComparison.OrdinalIgnoreCase))
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Usage: break <list|set|clear>");
				return false;
			}

			if (!TryParseAddress(args[1], out uint address))
			{
				Console.WriteLine($"Invalid address: {args[1]}");
				return false;
			}

			if (BreakpointHit(address, Constants.AccessType.Execute))
			{
				Console.WriteLine($"Breakpoint already exists at address 0x{address:X8}");
				return false;
			}

			_breakpoints.Add(new Breakpoint(address, Constants.BreakpointType.Execute, true));

			return false;
		}

		if (string.Equals(args[0], "clear", StringComparison.OrdinalIgnoreCase))
		{
			if (args.Length < 2)
			{
				Console.WriteLine("Usage: break <list|set|clear>");
				return false;
			}

			if (!TryParseAddress(args[1], out uint address))
			{
				Console.WriteLine($"Invalid address: {args[1]}");
				return false;
			}

			for (int i = 0; i < _breakpoints.Count; i++)
			{
				var breakpoint = _breakpoints[i];
				if (breakpoint.Address == address && breakpoint.Type == Constants.BreakpointType.Execute)
				{
					_breakpoints.RemoveAt(i);
					return false;
				}
			}

			Console.WriteLine($"No Breakpoint found at address 0x{address:X8}");
			return false;
		}

		Console.WriteLine("Usage: break <list|set|clear>");
		return false;
	}
}
