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

	public Result<byte?> Read(uint address)
	{
		address %= (uint)_data.Length;

		byte value = _data[address];
		return new Result<byte?>(value);
	}

	public Result Write(uint address, byte value)
	{
		address %= (uint)_data.Length;

		Result result = new();

		if (_readOnly)
		{
			result.AddError(nameof(RAMDevice), "cannot write to read-only device");
		}
		else
		{
			_data[address] = value;
		}

		return result;
	}
}
