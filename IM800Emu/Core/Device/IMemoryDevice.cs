namespace IM800Emu.Core.Device;

public interface IMemoryDevice
{
	public uint Length { get; }
	Result<byte?> Read(uint address);

	Result Write(uint address, byte value);
}
