using System.Buffers.Binary;

namespace IM800Emu.Core.Device;

internal class RAMDevice : IMemoryDevice
{
	private readonly byte[] _data;
	private readonly bool _readOnly;

	public RAMDevice(byte[] data, bool readOnly)
	{
		_data = data;
		_readOnly = readOnly;
	}

	public RAMDevice(int size)
	{
		_data = new byte[size];
		_readOnly = false;
		Array.Fill<byte>(_data, 0xFF);
	}

	public uint Length => (uint)_data.Length;

	public Result<uint?> Read(uint address, Constants.DataSize size)
	{
		address %= (uint)_data.Length;

		uint? value = size switch
		{
			Constants.DataSize.Byte => _data[address],
			Constants.DataSize.Word => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan((int)address)),
			Constants.DataSize.Dword => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan((int)address)),
			_ => null,
		};

		return new Result<uint?>(value);
	}

	public Result Write(uint address, Constants.DataSize size, uint value)
	{
		address %= (uint)_data.Length;

		Result result = new();

		if (_readOnly)
		{
			result.AddError(nameof(RAMDevice), "cannot write to read-only device");
		}
		else
		{
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

		return result;
	}
}
