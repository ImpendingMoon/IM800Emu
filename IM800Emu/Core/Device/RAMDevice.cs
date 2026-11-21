using System.Buffers.Binary;

namespace IM800Emu.Core.Device;

internal class RAMDevice : IMemoryDevice
{
	private readonly byte[] _data;
	private readonly bool _readOnly;
	private readonly string _name;
	private readonly Machine.MachineContext _context;

	public RAMDevice(byte[] data, bool readOnly, string name, Machine.MachineContext context)
	{
		_data = data;
		_readOnly = readOnly;
		_name = name;
		_context = context;
	}

	public RAMDevice(int size, string name, Machine.MachineContext context)
	{
		_data = new byte[size];
		_readOnly = false;
		Array.Fill<byte>(_data, 0xFF);

		_name = name;
		_context = context;
	}

	public uint Length => (uint)_data.Length;

	internal Span<byte> AsSpan()
	{
		return _data.AsSpan();
	}

	public uint Read(uint address, Constants.DataSize size)
	{
		if (address > Length - Helpers.SizeHelper.GetDataSizeBytes(size))
		{
			_context.Events.RaiseBeforeBadMemoryRead(address, size, _name);
			if (_context.CancelOperation)
			{
				return Constants.OpenBusValue;
			}

			// Still don't want to read out of bounds. Could change BinaryPrimitive to shift + OR later.
			return Constants.OpenBusValue;
		}

		uint value = size switch
		{
			Constants.DataSize.Byte => _data[address],
			Constants.DataSize.Word => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan((int)address)),
			Constants.DataSize.Dword => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan((int)address)),
			_ => Constants.OpenBusValue,
		};

		return value;
	}

	public void Write(uint address, Constants.DataSize size, uint value)
	{
		if (_readOnly || address > Length - Helpers.SizeHelper.GetDataSizeBytes(size))
		{
			_context.Events.RaiseBeforeBadMemoryWrite(address, size, value, _name);
			if (_context.CancelOperation)
			{
				return;
			}

			// Still don't want to write out of bounds. Could change BinaryPrimitive to shift + OR later.
			return;
		}

		switch (size)
		{
			case Constants.DataSize.Byte:
				_data[address] = (byte)value;
				break;
			case Constants.DataSize.Word:
				BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan((int)address), (ushort)value);
				break;
			case Constants.DataSize.Dword:
				BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan((int)address), value);
				break;
			default:
				break;
		}
	}
}
