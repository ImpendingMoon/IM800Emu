using IM800Emu.Core.Bus;
using IM800Emu.Core.CPU;
using IM800Emu.Core.Machine;
using System.Globalization;
using System.Text;

namespace IM800Emu.Core.IM800Debug;

internal partial class Debugger
{
	private const int SourceContextLines = 3;

	private readonly MachineContext _context;
	private readonly Dictionary<string, Func<string[], bool>> _commands;
	private bool _inDebugger;
	private bool _logExecution;

	// The debug file that's currently indexed. In case this ever can change without restarting the emulator.
	private DebugFile? _indexedDebugFile;
	private List<DebugSymbol> _debugLabels = [];
	// All debug file symbols (labels, constants, etc.) by name, for command argument lookups
	private Dictionary<string, long> _symbolsByName = new(StringComparer.Ordinal);
	// Indices into DebugFile.SourceLines of lines that emit bytes, sorted by BaseAddress
	private List<int> _emittingLineIndices = [];
	private List<Breakpoint> _breakpoints;

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
			["break"] = HandleBreak,
			["b"] = HandleBreak,
			["help"] = HandleHelp,
			["?"] = HandleHelp
		};

		_breakpoints = new List<Breakpoint>();
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
			PrintSourceContext(_context.CurrentOperation.BaseAddress);
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
		if (!TryFindLabelAtOrBelow(address, out string? name, out uint labelAddress))
		{
			return address.ToString("X8");
		}

		uint offset = address - labelAddress;
		return $"{name}+{offset} ({address:X8})";
	}

	/// <summary>
	///     Gets a formatted block of source lines surrounding the line that was emitted at the
	///     given address, or null if one can't be found
	/// </summary>
	public string? GetSourceContext(uint address, int contextLines = SourceContextLines)
	{
		EnsureDebugIndex();

		DebugFile? debugFile = _indexedDebugFile;
		if (debugFile is null)
		{
			return null;
		}

		int currentIndex = FindSourceLineIndex(address);
		if (currentIndex < 0)
		{
			return null;
		}

		List<DebugSourceLine> lines = debugFile.SourceLines;
		int start = Math.Max(0, currentIndex - contextLines);
		int end = Math.Min(lines.Count - 1, currentIndex + contextLines);

		StringBuilder sb = new();
		int lastFileIndex = -1;

		for (int i = start; i <= end; i++)
		{
			DebugSourceLine line = lines[i];

			// Print the file name whenever it changes
			if (line.SourceFileNameIndex != lastFileIndex)
			{
				sb.AppendLine($"  -- {GetSourceFileName(line.SourceFileNameIndex)} --");
				lastFileIndex = line.SourceFileNameIndex;
			}

			string marker = i == currentIndex ? ">" : " ";
			string lineAddress = line.OutputLength > 0 ? line.BaseAddress.ToString("X8") : new string(' ', 8);
			string text = line.Text.TrimEnd('\r', '\n');

			sb.AppendLine($" {marker} {line.Line,5} {lineAddress} | {text}");
		}

		return sb.ToString();
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

	// Debug file helpers

	/// <summary>
	///     (Re)builds the lookup tables if the context's DebugFile has changed since the last call
	/// </summary>
	private void EnsureDebugIndex()
	{
		DebugFile? debugFile = _context.DebugFile;

		if (ReferenceEquals(debugFile, _indexedDebugFile))
		{
			return;
		}

		_indexedDebugFile = debugFile;
		_debugLabels = [];
		_emittingLineIndices = [];
		_symbolsByName = new(StringComparer.Ordinal);

		if (debugFile is null)
		{
			return;
		}

		foreach (DebugSymbol symbol in debugFile.Symbols)
		{
			_symbolsByName[symbol.Name] = symbol.Value; // Last definition wins on duplicates
		}

		_debugLabels = debugFile.Symbols
			.Where(s => s.SymbolType == Constants.SymbolType.Label)
			.OrderBy(s => s.Value)
			.ToList();

		_emittingLineIndices = Enumerable.Range(0, debugFile.SourceLines.Count)
			.Where(i => debugFile.SourceLines[i].OutputLength > 0)
			.OrderBy(i => debugFile.SourceLines[i].BaseAddress)
			.ToList();
	}

	/// <summary>
	///     Finds the last label at or below the address. Uses the debug file's symbols if one is
	///     loaded, otherwise falls back to the legacy symbol list on the context.
	/// </summary>
	private bool TryFindLabelAtOrBelow(uint address, out string? name, out uint labelAddress)
	{
		EnsureDebugIndex();

		name = null;
		labelAddress = 0;

		if (_indexedDebugFile is not null)
		{
			// Binary search for the last label with Value <= address
			int lo = 0;
			int hi = _debugLabels.Count - 1;
			int found = -1;

			while (lo <= hi)
			{
				int mid = lo + ((hi - lo) / 2);

				if (_debugLabels[mid].Value <= address)
				{
					found = mid;
					lo = mid + 1;
				}
				else
				{
					hi = mid - 1;
				}
			}

			if (found < 0)
			{
				return false;
			}

			name = _debugLabels[found].Name;
			labelAddress = (uint)_debugLabels[found].Value;
			return true;
		}

		// Legacy fallback. Symbols list is sorted on entry.
		Symbol? lastBelow = null;

		foreach (Symbol symbol in _context.Symbols)
		{
			if (symbol.Type != Constants.SymbolType.Label)
			{
				continue;
			}

			if ((uint)symbol.Value > address)
			{
				break;
			}

			lastBelow = symbol;
		}

		if (lastBelow is null)
		{
			return false;
		}

		name = lastBelow.Name;
		labelAddress = (uint)lastBelow.Value;
		return true;
	}

	/// <summary>
	///     Finds the index into DebugFile.SourceLines of the line whose emitted bytes contain
	///     the address, or -1 if there is none.
	/// </summary>
	private int FindSourceLineIndex(uint address)
	{
		DebugFile? debugFile = _indexedDebugFile;
		if (debugFile is null)
		{
			return -1;
		}

		// Binary search for the last emitting line with BaseAddress <= address
		int lo = 0;
		int hi = _emittingLineIndices.Count - 1;
		int found = -1;

		while (lo <= hi)
		{
			int mid = lo + ((hi - lo) / 2);

			if (debugFile.SourceLines[_emittingLineIndices[mid]].BaseAddress <= address)
			{
				found = mid;
				lo = mid + 1;
			}
			else
			{
				hi = mid - 1;
			}
		}

		if (found < 0)
		{
			return -1;
		}

		int lineIndex = _emittingLineIndices[found];
		DebugSourceLine line = debugFile.SourceLines[lineIndex];

		return address < line.BaseAddress + line.OutputLength ? lineIndex : -1;
	}

	private string GetSourceFileName(int index)
	{
		List<string>? names = _indexedDebugFile?.SourceFileNames;

		if (names is null || index < 0 || index >= names.Count)
		{
			return $"<unknown file {index}>";
		}

		return names[index];
	}

	private void PrintSourceContext(uint address)
	{
		string? context = GetSourceContext(address);

		if (context is not null)
		{
			Console.Write(context);
		}
	}

	// Parsing + dump helpers
	/// <summary>
	///     Parses a decimal number, a 0x-prefixed hex number, or a symbol (optionally with +/- offset)
	/// </summary>
	private bool TryParseNumber(string value, out int result)
	{
		if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return int.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out result);
		}

		if (int.TryParse(value, out result))
		{
			return true;
		}

		if (TryResolveSymbolExpression(value, out long symbolValue)
			&& symbolValue >= int.MinValue && symbolValue <= int.MaxValue)
		{
			result = (int)symbolValue;
			return true;
		}

		result = 0;
		return false;
	}

	/// <summary>
	///     Parses an address: a 0x-prefixed hex number, a symbol (optionally with +/- offset), or a decimal
	///     number.
	/// </summary>
	private bool TryParseAddress(string value, out uint result)
	{
		if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return uint.TryParse(value.AsSpan(2), NumberStyles.HexNumber, null, out result);
		}

		if (TryResolveSymbolExpression(value, out long symbolValue)
			&& symbolValue >= 0 && symbolValue <= uint.MaxValue)
		{
			result = (uint)symbolValue;
			return true;
		}

		return uint.TryParse(value, NumberStyles.None, null, out result);
	}

	/// <summary>
	///     Resolves "name", "name+offset" or "name-offset" to a symbol value. Offsets are decimal or
	///     0x-prefixed hex.
	/// </summary>
	private bool TryResolveSymbolExpression(string text, out long value)
	{
		if (TryGetSymbolValue(text, out value))
		{
			return true;
		}

		int split = text.LastIndexOfAny(['+', '-']);
		if (split > 0 && split < text.Length - 1
			&& TryGetSymbolValue(text[..split], out long baseValue)
			&& TryParseOffset(text[(split + 1)..], out long offset))
		{
			value = text[split] == '+' ? baseValue + offset : baseValue - offset;
			return true;
		}

		value = 0;
		return false;
	}

	private static bool TryParseOffset(string text, out long offset)
	{
		if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
		{
			return long.TryParse(text.AsSpan(2), NumberStyles.HexNumber, null, out offset);
		}

		return long.TryParse(text, NumberStyles.None, null, out offset);
	}

	/// <summary>
	///     Looks up a symbol of any type by exact name, in the debug file if loaded, otherwise in
	///     the legacy symbol list.
	/// </summary>
	private bool TryGetSymbolValue(string name, out long value)
	{
		EnsureDebugIndex();

		if (_indexedDebugFile is not null)
		{
			return _symbolsByName.TryGetValue(name, out value);
		}

		foreach (Symbol symbol in _context.Symbols)
		{
			if (string.Equals(symbol.Name, name, StringComparison.Ordinal))
			{
				value = symbol.Value;
				return true;
			}
		}

		value = 0;
		return false;
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
				if (i + j >= end)
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

	private bool BreakpointHit(uint address, Constants.AccessType accessType)
	{
		// TODO: ugly :(
		foreach (var breakpoint in _breakpoints)
		{
			if (!breakpoint.Active)
			{
				continue;
			}

			if (address == breakpoint.Address)
			{
				if (breakpoint.Type == Constants.BreakpointType.Execute && accessType == Constants.AccessType.Execute)
				{
					return true;
				}

				if ((breakpoint.Type == Constants.BreakpointType.Read
					|| breakpoint.Type == Constants.BreakpointType.ReadWrite)
					&& accessType == Constants.AccessType.Read)
				{
					return true;
				}

				if ((breakpoint.Type == Constants.BreakpointType.Write
					|| breakpoint.Type == Constants.BreakpointType.ReadWrite)
					&& accessType == Constants.AccessType.Write)
				{
					return true;
				}
			}
		}

		return false;
	}
}
