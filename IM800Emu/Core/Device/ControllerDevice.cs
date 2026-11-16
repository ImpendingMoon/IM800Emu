using IM800Emu.Core.Machine;

namespace IM800Emu.Core.Device;

internal class ControllerDevice : IMemoryDevice
{
	private readonly MachineContext _context;
	private readonly string _name;
	public uint Length => 2;

	public ControllerDevice(string name, MachineContext context)
	{
		_context = context;
		_name = name;
	}

	public uint Read(uint address, Constants.DataSize size)
	{
		uint value = Constants.OpenBusValue;

		if (size != Constants.DataSize.Byte)
		{
			_context.Events.RaiseBeforeBadMemoryRead(address, size, _name);
			if (_context.CancelOperation)
			{
				return value;
			}
		}

		if (address == 0)
		{
			value = ButtonInput.GetPressedButtonBitmap();
		}
		else
		{
			_context.Events.RaiseBeforeBadMemoryRead(address, size, _name);
			if (_context.CancelOperation)
			{
				return value;
			}
		}

		return value;
	}

	public void Write(uint address, Constants.DataSize size, uint value)
	{
		_context.Events.RaiseBeforeBadMemoryWrite(address, size, value, _name);
	}
}
