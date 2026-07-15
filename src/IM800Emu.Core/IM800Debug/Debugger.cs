using System.Text;
using IM800Emu.Core.Bus;
using IM800Emu.Core.Machine;

namespace IM800Emu.Core.IM800Debug;

public static class Debugger
{
	// Command name to handler. Handler returns true to break from the debugger
	private static readonly Dictionary<string, Func<MachineContext, string[], bool>> Commands = new(StringComparer.OrdinalIgnoreCase)
	{
		["continue"] = HandleContinue,
		["c"] = HandleContinue,

		["quit"] = HandleQuit,
		["q"] = HandleQuit,
		["exit"] = HandleQuit,

		["step"] = HandleStep,
		["s"] = HandleStep,

		["log"] = HandleLog,

		["regs"] = HandleDumpRegisters,
		["registers"] = HandleDumpRegisters,
		["r"] = HandleDumpRegisters,

		["stack"] = HandleDumpStack,
		["st"] = HandleDumpStack,

		["mem"] = HandleDumpMemory,
		["m"] = HandleDumpMemory,

		["help"] = HandleHelp,
		["?"] = HandleHelp,
	};

	public static void AttachDebugger(MachineContext context)
	{
		context.SetBreakpointInstructionHandler(HandleBreakpointInstruction);
		context.SetPauseStateHandler(HandlePauseState);
		context.SetRegisterDisplayStringHandlers(GetStandardRegisterDisplayString, GetFullRegisterDisplayString);
	}

	public static void HandlePauseState(MachineContext context)
	{
		if (!context.InDebugger)
		{
			context.InDebugger = true;

			Console.WriteLine("Emulator Paused.");

			string pcString = GetNamedAddress(context, context.CurrentOperation.BaseAddress);

			Console.WriteLine($"Currently executing {context.CurrentOperation} at {pcString}");
			Console.WriteLine();
			Console.WriteLine("Type 'help' for a list of commands.");
		}

		while (true)
		{
			Console.Write("dbg> ");
			string? line = Console.ReadLine();

			if (string.IsNullOrWhiteSpace(line))
			{
				continue;
			}

			string[] args = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

			if (args.Length == 0)
			{
				continue;
			}

			string commandName = args[0];
			args = args[1..]; // Remove to match C# main args convention

			if (Commands.TryGetValue(commandName, out Func<MachineContext, string[], bool>? handler))
			{
				bool shouldExit = handler(context, args);

				if (shouldExit)
				{
					break;
				}
			}
			else
			{
				Console.WriteLine($"Unknown command '{commandName}'. Type 'help' for a list of commands.");
			}
		}
	}

	private static bool HandleContinue(MachineContext context, string[] args)
	{
		context.Paused = false;
		context.SingleStep = false;
		context.LogExecution = false;
		context.InDebugger = false;
		return true; // Exit debugger
	}

	private static bool HandleQuit(MachineContext context, string[] args)
	{
		Environment.Exit(0);
		return true;
	}

	private static bool HandleStep(MachineContext context, string[] args)
	{
		context.SingleStep = true;
		context.LogExecution = true;
		return true;
	}

	private static bool HandleLog(MachineContext context, string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine($"Execution logging is currently {(context.LogExecution ? "ON" : "OFF")}.");
			return false;
		}

		switch (args[0].ToUpperInvariant())
		{
			case "ON":
			{
				context.LogExecution = true;
				Console.WriteLine("Execution logging enabled.");
				break;
			}
			case "OFF":
			{
				context.LogExecution = false;
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

	private static bool HandleDumpRegisters(MachineContext context, string[] args)
	{
		Console.WriteLine("==== REGISTER DUMP ====");
		Console.WriteLine(GetFullRegisterDisplayString(context));
		Console.WriteLine("========");
		return false;
	}

	private static bool HandleDumpStack(MachineContext context, string[] args)
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
		Console.WriteLine(GetStackDump(context, minus, plus, 4));
		Console.WriteLine("========");

		return false;
	}

	private static bool HandleDumpMemory(MachineContext context, string[] args)
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
		Console.WriteLine(GetMemoryDump(context, address, length, 16));
		Console.WriteLine("========");

		return false;
	}

	private static bool HandleHelp(MachineContext context, string[] args)
	{
		Console.WriteLine("Available commands:");
		Console.WriteLine("\tcontinue, c              Resume execution");
		Console.WriteLine("\tstep, s                  Single-step one instruction");
		Console.WriteLine("\tquit, q, exit            Quit the emulator");
		Console.WriteLine("\tlog <on|off>             Toggle execution logging");
		Console.WriteLine("\tregs, registers, r       Dump CPU registers");
		Console.WriteLine("\tstack, st [minus] [plus] Dump stack memory relative to SP (default -32 64)");
		Console.WriteLine("\tmem, m <addr> [length]   Dump a region of memory (default length 64)");
		Console.WriteLine("\thelp, ?                  Show this help text");
		return false;
	}

	private static bool TryParseNumber(string value, out int result)
	{
		if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return int.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out result);
		}

		return int.TryParse(value, out result);
	}

	private static bool TryParseAddress(string value, out uint result)
	{
		if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return uint.TryParse(value.AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out result);
		}

		return uint.TryParse(value, System.Globalization.NumberStyles.HexNumber, null, out result);
	}

	// 0 = Pause
	// 1 = Dump Registers + Stack
	// 2 = Log Execution On
	// 3 = Log Execution Off
	public static void HandleBreakpointInstruction(MachineContext context, uint baseAddress, uint code)
	{
		Console.WriteLine();

		string pcString = GetNamedAddress(context, baseAddress);

		Console.WriteLine($"==== Hit breakpoint {code} at {pcString} ====");

		switch (code)
		{
			case 0:
			{
				context.Paused = true;
				break;
			}
			case 1:
			{
				Console.WriteLine("==== REGISTER + STACK DUMP ====");
				Console.WriteLine(GetFullRegisterDisplayString(context));
				Console.WriteLine(GetStackDump(context, -32, 64, 4));
				Console.WriteLine("========");
				break;
			}
			case 2:
			{
				context.LogExecution = true;
				break;
			}
			case 3:
			{
				context.LogExecution = true;
				break;
			}
			default:
			{
				break;
			}
		}

		Console.WriteLine();
	}

	/// <summary>
	/// Gets a formatted string with a dump of stack memory from SP-minus to SP+plus
	/// </summary>
	/// <param name="plus"></param>
	/// <param name="minus"></param>
	/// <returns></returns>
	public static string GetStackDump(MachineContext context, int minus, int plus, int bytesPerLine)
	{
		uint sp = context.Cpu.Registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword);
		return GetMemoryDump(context, sp, minus, plus, bytesPerLine);
	}

	/// <summary>
	/// Gets a formatted string with a dump of memory starting at baseAddress for length bytes.
	/// </summary>
	public static string GetMemoryDump(MachineContext context, uint baseAddress, int length, int bytesPerLine)
	{
		return GetMemoryDump(context, baseAddress, 0, length, bytesPerLine, addStackLabels: false);
	}

	/// <summary>
	/// Gets a formatted string with a dump of memory relative to a base address, from base+minus to base+plus.
	/// Used for stack dumps, where entries are labeled as [SP+n] / [SP-n] and the SP row is marked.
	/// </summary>
	public static string GetMemoryDump(MachineContext context, uint baseAddress, int minus, int plus, int bytesPerLine)
	{
		return GetMemoryDump(context, baseAddress, minus, plus - minus, bytesPerLine, addStackLabels: true);
	}

	private static string GetMemoryDump(
		MachineContext context,
		uint baseAddress,
		int startOffset,
		int length,
		int bytesPerLine,
		bool addStackLabels
	)
	{
		StringBuilder sb = new();

		int i = startOffset;
		int end = startOffset + length;
		while (i <= end)
		{
			uint current = baseAddress + (uint)i;

			sb.Append('[');
			sb.Append(current.ToString("X8"));
			sb.Append("] ");

			if (addStackLabels)
			{
				sb.Append(i < 0 ? "[SP" : "[SP+");
				sb.Append(i);

				sb.Append(i >= 100 || i <= -100 ? "]\t" : "]\t\t");
			}

			char[] characters = new char[bytesPerLine];

			for (int j = 0; j < bytesPerLine; j++)
			{
				if (i + j >= length)
				{
					break;
				}

				Result<MemoryOperation> readResult = context.MemoryBus.Read(current, Constants.DataSize.Byte);

				if (readResult.IsSuccess)
				{
					byte value = (byte)readResult.ResultObject.Data;
					sb.Append(value.ToString("X2"));
					sb.Append(' ');
					characters[j] = value >= 0x20 && value <= 0x7F ? (char)value : '.';
				}
				else
				{
					sb.Append("xx ");
					characters[j] = '.';
				}

				current++;
			}

			sb.Append('\"');
			for (int j = 0; j < bytesPerLine; j++)
			{
				if (i + j >= length)
				{
					break;
				}

				sb.Append(characters[j]);
			}
			sb.Append('\"');

			if (addStackLabels && i == 0)
			{
				sb.Append(" <<<");
			}

			sb.AppendLine();
			i += bytesPerLine;
		}

		return sb.ToString();
	}

	public static string GetStandardRegisterDisplayString(MachineContext context)
	{
		StringBuilder sb = new();
		CPU.Registers registers = context.Cpu.Registers;

		sb.Append($"AF: {registers.Read(Constants.RegisterTarget.AF, Constants.DataSize.Dword):X8} ");
		sb.Append($"BC: {registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword):X8} ");
		sb.Append($"DE: {registers.Read(Constants.RegisterTarget.DE, Constants.DataSize.Dword):X8} ");
		sb.Append($"HL: {registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword):X8} ");
		sb.Append($"IX: {registers.Read(Constants.RegisterTarget.IX, Constants.DataSize.Dword):X8} ");
		sb.Append($"IY: {registers.Read(Constants.RegisterTarget.IY, Constants.DataSize.Dword):X8} ");
		sb.Append($"SP: {registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword):X8} ");

		string pcString = GetNamedAddress(
			context,
			registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword)
		);

		sb.Append($"PC: {pcString}");

		return sb.ToString();
	}

	public static string GetFullRegisterDisplayString(MachineContext context)
	{
		var sb = new StringBuilder();

		CPU.Registers registers = context.Cpu.Registers;
		sb.AppendLine("Registers:");
		sb.AppendLine(GetStandardRegisterDisplayString(context));
		sb.AppendLine("Alternate Registers:");
		sb.AppendLine(registers.GetAlternateDisplayString());
		sb.AppendLine("System Registers:");
		sb.AppendLine(registers.GetSystemDisplayString());
		sb.AppendLine("Flags:");
		sb.AppendLine(registers.GetFlagsDisplayString());

		return sb.ToString();
	}

	/// <summary>
	/// Gets a formatted string with the name of the previous symbol plus an offset
	/// </summary>
	/// <param name="address"></param>
	/// <returns></returns>
	public static string GetNamedAddress(MachineContext context, uint address)
	{
		Symbol? lastBelow = null;

		// Symbols list is sorted on entry
		for (int i = 0; i < context.Symbols.Count; i++)
		{
			Symbol symbol = context.Symbols[i];
			if (symbol.Type == Constants.SymbolType.Label)
			{
				if ((uint)symbol.Value > address)
				{
					if (lastBelow is null)
					{
						break;
					}

					uint offset = address - (uint)lastBelow.Value;
					return $"{lastBelow.Name}+{offset} ({address:X8})";
				}
				else
				{
					lastBelow = symbol;
				}
			}
		}

		return address.ToString("X8");
	}
}