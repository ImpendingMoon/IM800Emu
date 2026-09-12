namespace IM800Emu.Core.Device;

public interface IMemoryDevice
{
	public uint Length { get; }
	uint Read(uint address, Constants.DataSize size);

	void Write(uint address, Constants.DataSize size, uint value);
}
