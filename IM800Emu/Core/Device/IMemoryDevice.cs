namespace IM800Emu.Core.Device;

public interface IMemoryDevice
{
	public uint Length { get; }
	Result<uint?> Read(uint address, Constants.DataSize size);

	Result Write(uint address, Constants.DataSize size, uint value);
}
