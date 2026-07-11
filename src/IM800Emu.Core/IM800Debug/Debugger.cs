using System.Text;
using IM800Emu.Core.Bus;
using IM800Emu.Core.Machine;

namespace IM800Emu.Core.IM800Debug;

public class Debugger
{
	private MachineContext _context;

	public Debugger(MachineContext context)
	{
		_context = context;
		_context.SetBreakpointInstructionHandler(HandleBreakpointInstruction);
		_context.SetPauseStateHandler(HandlePauseState);
	}

	private void HandlePauseState(MachineContext context)
	{
		Console.WriteLine("Emulator Paused. Press Enter to Continue, Esc to Quit, S to step.");

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
		}
	}

	private void HandleBreakpointInstruction(MachineContext context, uint baseAddress, uint code)
	{
		Console.WriteLine();
		Console.WriteLine($"==== Hit BKPT {code} @ 0x{baseAddress:X8} ====");

		switch (code)
		{
			case 0:
			{
				context.Paused = true;
				break;
			}
			case 1:
			{
				Console.WriteLine("==== REGISTER DUMP ====");
				Console.WriteLine(context.Cpu.Registers.GetFullDisplayString());
				Console.WriteLine("========");
				break;
			}
			case 2:
			{
				Console.WriteLine("==== STACK DUMP ====");
				Console.WriteLine(GetStackDump(-32, 128));
				Console.WriteLine("========");
				break;
			}
			case 3:
			{
				Console.WriteLine("==== FULL DUMP ====");
				Console.WriteLine(context.Cpu.Registers.GetFullDisplayString());
				Console.WriteLine(GetStackDump(-32, 64));
				Console.WriteLine("========");
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
	private string GetStackDump(int minus, int plus)
	{
		StringBuilder sb = new();

		uint sp = _context.Cpu.Registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword);

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
				Result<MemoryOperation> readResult = _context.MemoryBus.Read(current, Constants.DataSize.Byte);

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

}