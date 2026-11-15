using IM800Emu.Core.Machine;

namespace IM800Emu.Core.Device;

/// <summary>
///     Memory-mapped console device using stdin/stdout.
///     Address map:
///     0 - STATUS (read)
///     - b0: RX_READY
///     - b1: TX_READY
///     1 - RX_DATA (read)
///     2 - TX_DATA (write)
/// </summary>
internal class ConsoleDevice : IMemoryDevice
{
	public const uint StatusOffset = 0;
	public const uint RxDataOffset = 1;
	public const uint TxDataOffset = 2;

	public const byte StatusRxReadyBit = 0b0000_0001;
	public const byte StatusTxReadyBit = 0b0000_0010;
	private readonly MachineContext _context;
	private readonly string _name;

	private readonly Stream _stdout = Console.OpenStandardOutput();

	public ConsoleDevice(string name, MachineContext context)
	{
		_name = name;
		_context = context;
		Console.WriteLine(
			"Virtual Console Device being used. Press Ctrl+D (EOF) to exit. Press Ctrl+P to enter the debugger."
		);
	}

	public uint Length => 4;

	public uint Read(uint address, Constants.DataSize size)
	{
		uint result = Constants.OpenBusValue;

		if (size != Constants.DataSize.Byte)
		{
			_context.Events.RaiseBeforeBadMemoryRead(address, size, _name);
			if (_context.CancelOperation)
			{
				return result;
			}
		}

		if (address == StatusOffset)
		{
			result = BuildStatus();
		}
		else if (address == RxDataOffset)
		{
			result = ReadRx();
		}
		else
		{
			_context.Events.RaiseBeforeBadMemoryRead(address, size, _name);
			if (_context.CancelOperation)
			{
				return result;
			}
		}

		return result;
	}

	public void Write(uint address, Constants.DataSize size, uint value)
	{
		if (size != Constants.DataSize.Byte)
		{
			_context.Events.RaiseBeforeBadMemoryWrite(address, size, value, _name);
			if (_context.CancelOperation)
			{
				return;
			}
		}

		if (address == TxDataOffset)
		{
			_stdout.WriteByte((byte)value);
			_stdout.Flush();
		}
		else
		{
			_context.Events.RaiseBeforeBadMemoryWrite(address, size, value, _name);
			if (_context.CancelOperation)
			{
				return;
			}
		}
	}

	private byte BuildStatus()
	{
		byte status = StatusTxReadyBit;

		//if (Console.KeyAvailable)
		//{
		//	status |= StatusRxReadyBit;
		//}

		return status;
	}

	private byte ReadRx()
	{
		if (!Console.KeyAvailable)
		{
			return 0x00;
		}

		ConsoleKeyInfo key = Console.ReadKey(true);

		char data = key.KeyChar;

		if (key.Key == ConsoleKey.Backspace)
		{
			data = '\b';
		}
		else if (key.Key == ConsoleKey.Enter)
		{
			data = '\n';
		}

		if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
		{
			if (key.Key == ConsoleKey.D)
			{
				//Environment.Exit(1);
			}
			else if (key.Key == ConsoleKey.P)
			{
				_context.MachineAction = Constants.MachineAction.Pause;
				return 0x00;
			}
		}

		return (byte)data;
	}
}
