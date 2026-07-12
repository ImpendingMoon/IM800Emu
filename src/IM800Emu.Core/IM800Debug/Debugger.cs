using System.Text;
using IM800Emu.Core.Bus;
using IM800Emu.Core.Machine;

namespace IM800Emu.Core.IM800Debug;

public static class Debugger
{
	public static void AttachDebugger(MachineContext context)
	{
		context.SetBreakpointInstructionHandler(HandleBreakpointInstruction);
		context.SetPauseStateHandler(HandlePauseState);
		context.SetRegisterDisplayStringHandlers(GetStandardRegisterDisplayString, GetFullRegisterDisplayString);
	}

	public static void HandlePauseState(MachineContext context)
	{
		Console.WriteLine("Emulator Paused.");
		string pcString = GetNamedAddress(context, context.CurrentOperation.BaseAddress);
		Console.WriteLine($"Currently Executing {context.CurrentOperation} at {pcString}");
		Console.WriteLine("Press Enter to Continue, Esc to Quit, S to step, D to dump.");

		while (true)
		{
			ConsoleKeyInfo key = Console.ReadKey(true);
			if (key.Key == ConsoleKey.Enter)
			{
				context.Paused = false;
				context.SingleStep = false;
				context.LogExecution = false;
				break;
			}
			else if (key.Key == ConsoleKey.Escape)
			{
				Environment.Exit(0);
			}
			else if (key.Key == ConsoleKey.S)
			{
				context.SingleStep = true;
				context.LogExecution = true;
				break;
			}
			else if (key.Key == ConsoleKey.D)
			{
				Console.WriteLine();
				Console.WriteLine("==== REGISTER + STACK DUMP ====");
				Console.WriteLine(GetFullRegisterDisplayString(context));
				Console.WriteLine(GetStackDump(context, -32, 64));
				Console.WriteLine("========");
			}
		}
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
				Console.WriteLine(GetStackDump(context, -32, 64));
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
	public static string GetStackDump(MachineContext context, int minus, int plus)
	{
		StringBuilder sb = new();

		uint sp = context.Cpu.Registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword);

		int i = minus;
		while (i <= plus)
		{
			uint current = sp + (uint)i;

			sb.Append('[');
			sb.Append(current.ToString("X8"));
			sb.Append("] ");

			if (i < 0)
			{
				sb.Append("[SP");
			}
			else
			{
				sb.Append("[SP+");
			}
			sb.Append(i);

			if (i >= 100 || i <= -100)
			{
				sb.Append("]\t");
			}
			else
			{
				sb.Append("]\t\t");
			}

			char[] characters = new char[4];

			for (int j = 0; j < 4; j++)
			{
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
			for (int j = 0; j < 4; j++)
			{
				sb.Append(characters[j]);
			}
			sb.Append('\"');

			if (i == 0)
			{
				sb.Append(" <<<");
			}

			sb.AppendLine();
			i += 4;
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
					return $"{lastBelow.Name}+{offset:X} ({address:X8})";
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