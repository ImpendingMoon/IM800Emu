namespace IM800Emu.Core.Device;

internal class ControllerDevice : IMemoryDevice
{
	public uint Length => 2;

	public uint Read(uint address, Constants.DataSize size)
	{
		return Input.GetPressedButtonBitmap();
	}

	public void Write(uint address, Constants.DataSize size, uint value)
	{
	}
}
