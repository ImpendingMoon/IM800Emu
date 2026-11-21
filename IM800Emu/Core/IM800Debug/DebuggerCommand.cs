using System.Text;

namespace IM800Emu.Core.IM800Debug;

internal partial class Debugger
{
	private const int DefaultMemoryReadLength = 32;
	private const int DefaultPortReadLength = 1;
	private const int DefaultStackBelow = 32;
	private const int DefaultStackAbove = 64;

	private static readonly (string Usage, string[] Description)[] HelpEntries =
	[
		("help", ["Prints all commands with their help text"]),
		("pause", ["Pauses the emulator"]),
		("continue", ["Resumes the emulator"]),
		("step [n]", ["Steps n instructions", "n defaults to 1"]),
		("reset", ["Resets the emulator"]),
		("exit", ["Exits the emulator"]),
		("trace <on/off>", ["Toggles instruction tracing"]),
		(
			"read <mem|port> <address> [length]",
			[
				"Reads length bytes from memory or I/O ports starting from address",
				"Length defaults to 32 for memory, 1 for ports"
			]
		),
		("read reg <register>", ["Reads the value of a register"]),
		("read regs", ["Reads the values of all registers"]),
		("read stack [below] [above]", ["Reads the bytes above and below the stack pointer"]),
		(
			"write <byte|word|dword> <mem|port> <address> <value>",
			["Writes a value to memory or I/O port at address"]
		),
		("write <byte|word|dword> reg <register> <value>", ["Writes a value to a register"]),
		("break list", ["Lists current breakpoints"]),
		("break <set|clear> <address>", ["Sets or clears a new breakpoint at address"]),
		("watch list", ["Lists current watchpoints"]),
		(
			"watch <set|clear> <r|w|rw> <address>",
			["Sets or clears a read and/or write watchpoint at address"]
		),
		("list [address] [count]", ["Lists source lines starting at address"]),
	];

	// Command handlers. Each returns true if the debugger prompt should be exited.

	private bool HandleHelp(string[] args)
	{
		Console.WriteLine("Available commands:");

		foreach ((string usage, string[] description) in HelpEntries)
		{
			Console.WriteLine(usage);

			foreach (string line in description)
			{
				Console.WriteLine($"\t- {line}");
			}

			Console.WriteLine();
		}

		return false;
	}

	private bool HandlePause(string[] args)
	{
		// No-op for now: the prompt is only reachable while the emulator is already paused.
		Console.WriteLine("Emulator is already paused.");
		return false;
	}

	private bool HandleContinue(string[] args)
	{
		_stepsRemaining = 0;
		_context.MachineAction = Constants.MachineAction.Continue;
		_logExecution = false;
		_inDebugger = false;
		return true; // Exit debugger
	}

	private bool HandleStep(string[] args)
	{
		int count = 1;

		if (args.Length >= 1 && (!TryParseNumber(args[0], out count) || count < 1))
		{
			Console.WriteLine("Usage: step [n]  (n must be a positive number)");
			return false;
		}

		// RunPauseState swallows the remaining steps without prompting
		_stepsRemaining = count - 1;
		_logExecution = true;
		return true;
	}

	private bool HandleReset(string[] args)
	{
		_stepsRemaining = 0;
		ResetMachine();

		Console.WriteLine("Emulator reset.");
		Console.WriteLine(GetStandardRegisterDisplayString());
		return false;
	}

	private bool HandleExit(string[] args)
	{
		Environment.Exit(0);
		return true;
	}

	private bool HandleTrace(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine($"Instruction tracing is currently {(_logExecution ? "ON" : "OFF")}.");
			return false;
		}

		switch (args[0].ToUpperInvariant())
		{
			case "ON":
			{
				_logExecution = true;
				Console.WriteLine("Instruction tracing enabled.");
				break;
			}
			case "OFF":
			{
				_logExecution = false;
				Console.WriteLine("Instruction tracing disabled.");
				break;
			}
			default:
			{
				Console.WriteLine("Usage: trace <on/off>");
				break;
			}
		}

		return false;
	}

	// read

	private bool HandleRead(string[] args)
	{
		if (args.Length == 0)
		{
			PrintReadUsage();
			return false;
		}

		string[] rest = args[1..];

		switch (args[0].ToUpperInvariant())
		{
			case "MEM":
			{
				ReadMemoryOrPort(rest, isPort: false);
				break;
			}
			case "PORT":
			{
				ReadMemoryOrPort(rest, isPort: true);
				break;
			}
			case "REG":
			{
				ReadRegister(rest);
				break;
			}
			case "REGS":
			{
				Console.WriteLine(GetFullRegisterDisplayString());
				break;
			}
			case "STACK":
			{
				ReadStack(rest);
				break;
			}
			default:
			{
				PrintReadUsage();
				break;
			}
		}

		return false;
	}

	private static void PrintReadUsage()
	{
		Console.WriteLine("Usage: read <mem|port> <address> [length]");
		Console.WriteLine("       read reg <register>");
		Console.WriteLine("       read regs");
		Console.WriteLine("       read stack [below] [above]");
	}

	private void ReadMemoryOrPort(string[] args, bool isPort)
	{
		string kind = isPort ? "port" : "mem";

		if (args.Length < 1 || args.Length > 2)
		{
			Console.WriteLine($"Usage: read {kind} <address> [length]");
			return;
		}

		if (!TryParseAddress(args[0], out uint address))
		{
			Console.WriteLine($"Invalid address: {args[0]}");
			return;
		}

		int length = isPort ? DefaultPortReadLength : DefaultMemoryReadLength;
		if (args.Length >= 2 && !TryParseNumber(args[1], out length))
		{
			Console.WriteLine($"Invalid length: {args[1]}");
			return;
		}

		if (length <= 0)
		{
			Console.WriteLine("Length must be positive.");
			return;
		}

		Console.WriteLine(isPort ? GetPortDump(address, length, 16) : GetMemoryDump(address, length, 16));
	}

	private void ReadRegister(string[] args)
	{
		if (args.Length != 1)
		{
			Console.WriteLine("Usage: read reg <register>");
			return;
		}

		if (!TryParseRegister(args[0], out Constants.RegisterTarget target))
		{
			Console.WriteLine($"Unknown register: {args[0]}");
			return;
		}

		Constants.DataSize size = GetNativeRegisterSize(target);
		uint value = _context.Cpu.Registers.Read(target, size);
		int digits = size == Constants.DataSize.Word ? 4 : 8;

		string display = target == Constants.RegisterTarget.PC
			? GetNamedAddress(value)
			: $"{value.ToString("X" + digits)}";

		char character = (value >= ' ' && value <= '~') ? (char)value : ' ';

		Console.WriteLine($"{target.ToString().Replace('_', '\'')} = {display} ({value}) '{character}'");
	}

	private void ReadStack(string[] args)
	{
		int below = DefaultStackBelow;
		int above = DefaultStackAbove;

		if (args.Length > 2)
		{
			Console.WriteLine("Usage: read stack [below] [above]");
			return;
		}

		if (args.Length >= 1 && (!TryParseNumber(args[0], out below) || below < 0))
		{
			Console.WriteLine($"Invalid value for 'below': {args[0]}");
			return;
		}

		if (args.Length >= 2 && (!TryParseNumber(args[1], out above) || above < 0))
		{
			Console.WriteLine($"Invalid value for 'above': {args[1]}");
			return;
		}

		if (below + above == 0)
		{
			Console.WriteLine("Nothing to read.");
			return;
		}

		Console.WriteLine(GetStackDump(-below, above, 4));
	}

	// write

	private bool HandleWrite(string[] args)
	{
		if (args.Length != 4 || !TryParseDataSize(args[0], out Constants.DataSize size))
		{
			PrintWriteUsage();
			return false;
		}

		string target = args[1].ToUpperInvariant();

		if (target is not ("MEM" or "PORT" or "REG"))
		{
			PrintWriteUsage();
			return false;
		}

		if (!TryParseAddress(args[3], out uint value))
		{
			Console.WriteLine($"Invalid value: {args[3]}");
			return false;
		}

		if (!FitsInSize(value, size))
		{
			Console.WriteLine($"Value {args[3]} does not fit in a {size.ToString().ToLowerInvariant()}.");
			return false;
		}

		switch (target)
		{
			case "REG":
			{
				if (!TryParseRegister(args[2], out Constants.RegisterTarget register))
				{
					Console.WriteLine($"Unknown register: {args[2]}");
					return false;
				}

				_context.Cpu.Registers.Write(register, size, value);
				Console.WriteLine(
					$"{register.ToString().Replace('_', '\'')} <- {FormatValue(value, size)}"
				);
				break;
			}
			default:
			{
				if (!TryParseAddress(args[2], out uint address))
				{
					Console.WriteLine($"Invalid address: {args[2]}");
					return false;
				}

				if (target == "MEM")
				{
					WriteMemory(address, value, size);
					Console.WriteLine($"[{address:X8}] <- {FormatValue(value, size)}");
				}
				else
				{
					WritePort(address, value, size);
					Console.WriteLine($"port {address:X8} <- {FormatValue(value, size)}");
				}

				break;
			}
		}

		return false;
	}

	private static void PrintWriteUsage()
	{
		Console.WriteLine("Usage: write <byte|word|dword> <mem|port> <address> <value>");
		Console.WriteLine("       write <byte|word|dword> reg <register> <value>");
	}

	// break

	private bool HandleBreak(string[] args)
	{
		if (args.Length == 0)
		{
			PrintBreakUsage();
			return false;
		}

		switch (args[0].ToUpperInvariant())
		{
			case "LIST" when args.Length == 1:
			{
				if (_breakpoints.Count == 0)
				{
					Console.WriteLine("No breakpoints set.");
					break;
				}

				Console.WriteLine("Breakpoints:");
				foreach (Breakpoint breakpoint in _breakpoints.Values.OrderBy(b => b.Address))
				{
					Console.WriteLine($"\t{GetNamedAddress(breakpoint.Address)}");
				}

				break;
			}
			case "SET" when args.Length == 2:
			case "CLEAR" when args.Length == 2:
			{
				if (!TryParseAddress(args[1], out uint address))
				{
					Console.WriteLine($"Invalid address: {args[1]}");
					break;
				}

				string named = GetNamedAddress(address);

				if (args[0].Equals("set", StringComparison.OrdinalIgnoreCase))
				{
					if (_breakpoints.TryAdd(address, new Breakpoint(address)))
					{
						Console.WriteLine($"Breakpoint set at {named}.");
					}
					else
					{
						Console.WriteLine($"Breakpoint already set at {named}.");
					}
				}
				else if (_breakpoints.Remove(address))
				{
					Console.WriteLine($"Breakpoint cleared at {named}.");
				}
				else
				{
					Console.WriteLine($"No breakpoint set at {named}.");
				}

				break;
			}
			default:
			{
				PrintBreakUsage();
				break;
			}
		}

		return false;
	}

	private static void PrintBreakUsage()
	{
		Console.WriteLine("Usage: break list");
		Console.WriteLine("       break <set|clear> <address>");
	}

	// watch

	private bool HandleWatch(string[] args)
	{
		if (args.Length == 0)
		{
			PrintWatchUsage();
			return false;
		}

		string action = args[0].ToUpperInvariant();

		if (action == "LIST" && args.Length == 1)
		{
			if (_watchpoints.Count == 0)
			{
				Console.WriteLine("No watchpoints set.");
				return false;
			}

			Console.WriteLine("Watchpoints:");
			foreach (Watchpoint watchpoint in _watchpoints.Values.OrderBy(w => w.Address))
			{
				Console.WriteLine(
					$"\t{GetNamedAddress(watchpoint.Address)}  {FormatWatchMode(watchpoint.BreakOnRead, watchpoint.BreakOnWrite)}"
				);
			}

			return false;
		}

		if (action is not ("SET" or "CLEAR") || args.Length != 3)
		{
			PrintWatchUsage();
			return false;
		}

		bool read;
		bool write;
		switch (args[1].ToUpperInvariant())
		{
			case "R":
			{
				read = true;
				write = false;
				break;
			}
			case "W":
			{
				read = false;
				write = true;
				break;
			}
			case "RW":
			case "WR":
			{
				read = true;
				write = true;
				break;
			}
			default:
			{
				PrintWatchUsage();
				return false;
			}
		}

		if (!TryParseAddress(args[2], out uint address))
		{
			Console.WriteLine($"Invalid address: {args[2]}");
			return false;
		}

		string named = GetNamedAddress(address);
		_watchpoints.TryGetValue(address, out Watchpoint? existing);

		if (action == "SET")
		{
			if (existing is null)
			{
				existing = new Watchpoint(address, read, write);
				_watchpoints[address] = existing;
			}
			else
			{
				existing.BreakOnRead |= read;
				existing.BreakOnWrite |= write;
			}

			Console.WriteLine(
				$"Watchpoint at {named} now breaks on {FormatWatchMode(existing.BreakOnRead, existing.BreakOnWrite)}."
			);
		}
		else
		{
			if (existing is null)
			{
				Console.WriteLine($"No watchpoint set at {named}.");
				return false;
			}

			existing.BreakOnRead &= !read;
			existing.BreakOnWrite &= !write;

			if (!existing.BreakOnRead && !existing.BreakOnWrite)
			{
				_watchpoints.Remove(address);
				Console.WriteLine($"Watchpoint cleared at {named}.");
			}
			else
			{
				Console.WriteLine(
					$"Watchpoint at {named} now breaks on {FormatWatchMode(existing.BreakOnRead, existing.BreakOnWrite)}."
				);
			}
		}

		return false;
	}

	private static void PrintWatchUsage()
	{
		Console.WriteLine("Usage: watch list");
		Console.WriteLine("       watch <set|clear> <r|w|rw> <address>");
	}

	private static string FormatWatchMode(bool read, bool write)
	{
		return (read, write) switch
		{
			(true, true) => "read/write",
			(true, false) => "read",
			(false, true) => "write",
			_ => "nothing"
		};
	}

	// list

	private bool HandleList(string[] args)
	{
		if (args.Length >= 1 && string.Equals(args[0], "labels", StringComparison.OrdinalIgnoreCase))
		{
			return HandleListLabels(args);
		}

		uint address = _context.Cpu.Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		int before = Config.DebugContextLines;
		int after = Config.DebugContextLines;
		bool printMarker = true;

		if (args.Length >= 1)
		{
			before = 0;
			printMarker = false;
			if (!TryParseAddress(args[0], out address))
			{
				Console.WriteLine($"Invalid address: {args[0]}");
				return false;
			}
		}

		if (args.Length >= 2)
		{
			before = 0;
			printMarker = false;
			if (!TryParseNumber(args[1], out after))
			{
				Console.WriteLine($"Invalid count: {args[0]}");
				return false;
			}
		}

		string? context = GetSourceContext(address, before, after, printMarker);
		if (context is null)
		{
			Console.WriteLine($"Could not find source lines for address 0x{address:X8}");
		}
		else
		{
			Console.Write(context);
		}

		return false;
	}

	private static void PrintListUsage()
	{
		Console.WriteLine("Usage: list [address] [count]");
	}

	private bool HandleListLabels(string[] args)
	{
		List<DebugSymbol> labels = new List<DebugSymbol>();

		if (args.Length >= 2)
		{
			string searchString = args[1];
			foreach (var label in _debugLabels)
			{
				if (label.Name.Contains(searchString))
				{
					labels.Add(label);
				}
			}
		}
		else
		{
			labels = _debugLabels;
		}

		foreach (var label in labels)
		{
			Console.WriteLine(label);
		}

		return false;
	}

	// Parsing / formatting helpers

	private static bool TryParseDataSize(string text, out Constants.DataSize size)
	{
		switch (text.ToUpperInvariant())
		{
			case "BYTE":
			{
				size = Constants.DataSize.Byte;
				return true;
			}
			case "WORD":
			{
				size = Constants.DataSize.Word;
				return true;
			}
			case "DWORD":
			{
				size = Constants.DataSize.Dword;
				return true;
			}
			default:
			{
				size = Constants.DataSize.None;
				return false;
			}
		}
	}

	private static bool FitsInSize(uint value, Constants.DataSize size)
	{
		return size switch
		{
			Constants.DataSize.Byte => value <= byte.MaxValue,
			Constants.DataSize.Word => value <= ushort.MaxValue,
			_ => true
		};
	}

	private static string FormatValue(uint value, Constants.DataSize size)
	{
		return size switch
		{
			Constants.DataSize.Byte => $"0x{value:X2}",
			Constants.DataSize.Word => $"0x{value:X4}",
			_ => $"0x{value:X8}"
		};
	}

	/// <summary>
	///     Parses a register name (case-insensitive). Alternate registers are written with an
	///     apostrophe, e.g. af'
	/// </summary>
	private static bool TryParseRegister(string name, out Constants.RegisterTarget target)
	{
		string normalized = name.Replace('\'', '_');

		if (normalized.Length == 0
			|| !char.IsLetter(normalized[0])
			|| !Enum.TryParse(normalized, true, out target)
			|| !Enum.IsDefined(target)
			|| target == Constants.RegisterTarget.None)
		{
			target = Constants.RegisterTarget.None;
			return false;
		}

		return true;
	}

	private static Constants.DataSize GetNativeRegisterSize(Constants.RegisterTarget target)
	{
		return target switch
		{
			Constants.RegisterTarget.A or Constants.RegisterTarget.F
				or Constants.RegisterTarget.B or Constants.RegisterTarget.C
				or Constants.RegisterTarget.D or Constants.RegisterTarget.E
				or Constants.RegisterTarget.H or Constants.RegisterTarget.L
				or Constants.RegisterTarget.A_ or Constants.RegisterTarget.F_
				or Constants.RegisterTarget.B_ or Constants.RegisterTarget.C_
				or Constants.RegisterTarget.D_ or Constants.RegisterTarget.E_
				or Constants.RegisterTarget.H_ or Constants.RegisterTarget.L_
				or Constants.RegisterTarget.R
				=> Constants.DataSize.Word,
			_ => Constants.DataSize.Dword
		};
	}

	private string GetPortDump(uint baseAddress, int length, int bytesPerLine)
	{
		StringBuilder sb = new();

		for (int i = 0; i < length; i += bytesPerLine)
		{
			sb.Append($"[{baseAddress + (uint)i:X8}] ");

			for (int j = 0; j < bytesPerLine && i + j < length; j++)
			{
				byte value = (byte)ReadPort(baseAddress + (uint)(i + j));
				sb.Append(value.ToString("X2"));
				sb.Append(' ');
			}

			sb.AppendLine();
		}

		return sb.ToString();
	}


	private void ResetMachine()
	{
		// TODO
	}

	private uint ReadPort(uint address)
	{
		return (uint)_context.IoBus.Read(address, Constants.DataSize.Byte).Data;
	}

	private void WritePort(uint address, uint value, Constants.DataSize size)
	{
		_context.IoBus.Write(address, size, value);
	}

	private void WriteMemory(uint address, uint value, Constants.DataSize size)
	{
		_context.MemoryBus.Write(address, size, value);
	}
}
