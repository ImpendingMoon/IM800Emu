namespace IM800Emu.Core.Device;

public interface IMemoryDevice
{
	Result<byte?> Read(uint address);

	Result Write(uint address, byte value);

	uint Length { get; }
}
