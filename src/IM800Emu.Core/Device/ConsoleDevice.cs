namespace IM800Emu.Core.Device;

/// <summary>
/// Memory-mapped console device using stdin/stdout.
/// Address map:
/// 0 - STATUS (read)
/// - b0: RX_READY
/// - b1: TX_READY
/// 1 - RX_DATA (read)
/// 2 - TX_DATA (write)
/// </summary>
internal class ConsoleDevice : IMemoryDevice
{
	public const uint StatusOffset = 0;
	public const uint RxDataOffset = 1;
	public const uint TxDataOffset = 2;

	public const byte StatusRxReadyBit = 0b0000_0001;
	public const byte StatusTxReadyBit = 0b0000_0010;

	private readonly Stream _stdout = Console.OpenStandardOutput();

	public ConsoleDevice()
	{
		Console.WriteLine("Virtual Console Device being used. Press Ctrl+D (EOF) to exit.");
	}

	public uint Length => 4;

	private static byte BuildStatus()
	{
		byte status = StatusTxReadyBit;

		if (Console.KeyAvailable)
		{
			status |= StatusRxReadyBit;
		}

		return status;
	}

	private static byte ReadRx()
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

		if (key.Key == ConsoleKey.D && key.Modifiers.HasFlag(ConsoleModifiers.Control))
		{
			Environment.Exit(1);
		}

		return (byte)data;
	}

	public Result<byte?> Read(uint address)
	{
		Result<byte?> result = new(0);
		uint offset = address % Length;

		if (offset == StatusOffset)
		{
			result.ResultObject = BuildStatus();
		}
		else if (offset == RxDataOffset)
		{
			result.ResultObject = ReadRx();
		}
		else
		{
			result.AddError("ConsoleDevice", $"invalid read address {address}");
		}

		return result;
	}

	public Result Write(uint address, byte value)
	{
		Result result = new();
		uint offset = address % Length;

		if (offset == TxDataOffset)
		{
			_stdout.WriteByte((byte)value);
			_stdout.Flush();
		}
		else
		{
			result.AddError("ConsoleDevice", $"invalid write address {address}");
		}

		return result;
	}
}
