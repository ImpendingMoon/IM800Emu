using IM800Emu.Core.Bus;
using IM800Emu.Core.CPU;
using IM800Emu.Core.Machine;
using System.Globalization;
using System.Text;

namespace IM800Emu.Core.IM800Debug;

public class Debugger
{
	private readonly MachineContext _context;
	private readonly Dictionary<string, Func<string[], bool>> _commands;
	private bool _inDebugger;
	private bool _logExecution;

	public Debugger(MachineContext context)
	{
		_context = context;
		_inDebugger = false;
		_logExecution = false;

		_context.Events.BeforeMemoryRead += OnBeforeMemoryRead;
		_context.Events.BeforeMemoryWrite += OnBeforeMemoryWrite;
		_context.Events.BeforeBadMemoryRead += OnBeforeBadMemoryRead;
		_context.Events.BeforeBadMemoryWrite += OnBeforeBadMemoryWrite;

		_context.Events.BeforeInstructionFetch += OnBeforeInstructionFetch;
		_context.Events.InvalidInstruction += OnInvalidInstruction;
		_context.Events.BkptInstruction += OnBkptInstruction;

		_context.Events.StepFinished += OnStepFinished;
		_context.Events.FrameFinished += OnFrameFinished;

		_commands = new(StringComparer.OrdinalIgnoreCase)
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
			["?"] = HandleHelp
		};
	}

	~Debugger()
	{
		_context.Events.BeforeMemoryRead -= OnBeforeMemoryRead;
		_context.Events.BeforeMemoryWrite -= OnBeforeMemoryWrite;
		_context.Events.BeforeBadMemoryRead -= OnBeforeBadMemoryRead;
		_context.Events.BeforeBadMemoryWrite -= OnBeforeBadMemoryWrite;

		_context.Events.BeforeInstructionFetch -= OnBeforeInstructionFetch;
		_context.Events.InvalidInstruction -= OnInvalidInstruction;
		_context.Events.BkptInstruction -= OnBkptInstruction;

		_context.Events.StepFinished -= OnStepFinished;
		_context.Events.FrameFinished -= OnFrameFinished;
	}

	public void CheckForPauseHotkey()
	{
		if (Console.KeyAvailable)
		{
			var key = Console.ReadKey();
			if (key.Modifiers.HasFlag(ConsoleModifiers.Control) && key.Key == ConsoleKey.P)
			{
				_context.MachineAction = Constants.MachineAction.Pause;
			}
		}
	}

	public void RunPauseState()
	{
		if (!_inDebugger)
		{
			_inDebugger = true;

			Console.WriteLine("Emulator Paused.");

			string pcString = GetNamedAddress(_context.CurrentOperation.BaseAddress);

			Console.WriteLine($"Currently executing {_context.CurrentOperation} at {pcString}");
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

			if (_commands.TryGetValue(commandName, out Func<string[], bool>? handler))
			{
				bool shouldExit = handler(args);

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

	/// <summary>
	///     Gets a formatted string with the name of the previous symbol plus an offset
	/// </summary>
	/// <param name="address"></param>
	/// <returns></returns>
	public string GetNamedAddress(uint address)
	{
		Symbol? lastBelow = null;

		// Symbols list is sorted on entry
		for (int i = 0; i < _context.Symbols.Count; i++)
		{
			Symbol symbol = _context.Symbols[i];
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

				lastBelow = symbol;
			}
		}

		return address.ToString("X8");
	}

	public string GetStandardRegisterDisplayString()
	{
		StringBuilder sb = new();
		Registers registers = _context.Cpu.Registers;

		sb.Append($"AF: {registers.Read(Constants.RegisterTarget.AF, Constants.DataSize.Dword):X8} ");
		sb.Append($"BC: {registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword):X8} ");
		sb.Append($"DE: {registers.Read(Constants.RegisterTarget.DE, Constants.DataSize.Dword):X8} ");
		sb.Append($"HL: {registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword):X8} ");
		sb.Append($"IX: {registers.Read(Constants.RegisterTarget.IX, Constants.DataSize.Dword):X8} ");
		sb.Append($"IY: {registers.Read(Constants.RegisterTarget.IY, Constants.DataSize.Dword):X8} ");
		sb.Append($"SP: {registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword):X8} ");

		string pcString = GetNamedAddress(
			registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword)
		);

		sb.Append($"PC: {pcString}");

		return sb.ToString();
	}

	public string GetFullRegisterDisplayString()
	{
		var sb = new StringBuilder();

		Registers registers = _context.Cpu.Registers;
		sb.AppendLine("Registers:");
		sb.AppendLine(GetStandardRegisterDisplayString());
		sb.AppendLine("Alternate Registers:");
		sb.AppendLine(registers.GetAlternateDisplayString());
		sb.AppendLine("System Registers:");
		sb.AppendLine(registers.GetSystemDisplayString());
		sb.AppendLine("Flags:");
		sb.AppendLine(registers.GetFlagsDisplayString());

		return sb.ToString();
	}

	// Event handlers
	private void OnBeforeMemoryRead(object? sender, MachineEvents.BeforeMemoryReadArgs args)
	{
		if (_inDebugger)
		{
			return;
		}

		// TODO: Watchpoints
	}

	private void OnBeforeMemoryWrite(object? sender, MachineEvents.BeforeMemoryWriteArgs args)
	{
		if (_inDebugger)
		{
			return;
		}

		// TODO: Watchpoints
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

		// TODO: Breakpoints
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

	// Helpers
	private static bool TryParseNumber(string value, out int result)
	{
		if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out result);
		}

		return int.TryParse(value, out result);
	}

	private static bool TryParseAddress(string value, out uint result)
	{
		if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out result);
		}

		return uint.TryParse(value, NumberStyles.HexNumber, null, out result);
	}

	private string GetStackDump(int minus, int plus, int bytesPerLine)
	{
		uint sp = _context.Cpu.Registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword);
		return GetMemoryDump(sp, minus, plus, bytesPerLine);
	}

	private string GetMemoryDump(uint baseAddress, int length, int bytesPerLine)
	{
		return GetMemoryDump(baseAddress, 0, length, bytesPerLine, false);
	}

	private string GetMemoryDump(uint baseAddress, int minus, int plus, int bytesPerLine)
	{
		return GetMemoryDump(baseAddress, minus, plus - minus, bytesPerLine, true);
	}

	private string GetMemoryDump
	(
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
		while (i < end)
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

				MemoryResult readResult = _context.MemoryBus.Read(current, Constants.DataSize.Byte);

				byte value = (byte)readResult.Data;
				sb.Append(value.ToString("X2"));
				sb.Append(' ');
				characters[j] = value >= 0x20 && value < 0x7F ? (char)value : '.';

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
}
