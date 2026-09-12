namespace IM800Emu.Core.Device;

internal class ControllerDevice : IMemoryDevice
{
	public uint Length => 2;

	public Result<uint?> Read(uint address, Constants.DataSize size)
	{
		uint buttonState = Input.GetPressedButtonBitmap();
		return new(buttonState);
	}

	public Result Write(uint address, Constants.DataSize size, uint value)
	{
		Result result = new();
		result.AddError("ControllerDevice", "Cannot write to ControllerDevice");
		return result;
	}
}
