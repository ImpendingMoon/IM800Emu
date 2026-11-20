using IM800Emu.Core.Machine;
using Raylib_cs;

namespace IM800Emu.Core.Device;

internal class KeyboardDevice : IMemoryDevice, IInterruptingDevice
{
	private MachineContext _context;
	private string _name;

	private bool _enableInterrupts;

	private readonly Queue<byte> _buffer;
	private readonly Dictionary<KeyboardKey, int> _currentlyPressedKeys;

	public KeyboardDevice(string name, MachineContext context)
	{
		_context = context;
		_name = name;
		_enableInterrupts = false;
		_buffer = [];
		_currentlyPressedKeys = [];
	}

	public uint Length => 4;

	public bool INT { get; private set; }

	public bool NMI => false;

	public bool IsServicingInterrupt { get; private set; }

	public byte OnInterruptAcknowledge()
	{
		INT = false;
		IsServicingInterrupt = true;
		return Constants.KeyboardInterruptNumber;
	}

	public void OnInterruptComplete()
	{
		IsServicingInterrupt = false;
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
			if (_buffer.Count > 0)
			{
				value = _buffer.Dequeue();
			}
			else
			{
				value = 0;
			}
		}
		else if (address == 1)
		{
			value = (uint)(_enableInterrupts ? 1 : 0);
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
		if (size != Constants.DataSize.Byte)
		{
			_context.Events.RaiseBeforeBadMemoryWrite(address, size, value, _name);
			if (_context.CancelOperation)
			{
				return;
			}
		}

		if (address == 1)
		{
			_enableInterrupts = (value & 1) != 0;
		}
		else
		{
			_context.Events.RaiseBeforeBadMemoryWrite(address, size, value, _name);
			if (_context.CancelOperation)
			{
				return;
			}
		}
	}

	public void GetInputFromWindow()
	{
		var key = (KeyboardKey)Raylib.GetKeyPressed();

		while (key != KeyboardKey.Null)
		{
			_currentlyPressedKeys[key] = 0;
			byte[] scancode = KeyToScancode(key);
			if (scancode.Length > 0)
			{
				AddKey(scancode);
			}

			key = (KeyboardKey)Raylib.GetKeyPressed();
		}

		List<KeyboardKey> keysToRemove = [];

		foreach ((KeyboardKey pressedKey, int heldFrames) in _currentlyPressedKeys)
		{
			// Prevent stuck keys on focus loss
			if (!Raylib.IsWindowFocused() || Raylib.IsKeyReleased(pressedKey))
			{
				keysToRemove.Add(pressedKey);
				byte[] scancode = KeyToScancode(pressedKey);
				if (scancode.Length > 0)
				{
					if (pressedKey == KeyboardKey.PrintScreen)
					{
						AddKey([0xE0, 0xB7, 0xE0, 0xAA]);
					}
					else if (pressedKey != KeyboardKey.Pause)
					{
						// Set break bit on the last byte of the scancode
						scancode[^1] |= 0x80;
						AddKey(scancode);
					}
				}
			}
			else
			{
				_currentlyPressedKeys[pressedKey] = heldFrames + 1;
			}
		}

		foreach (KeyboardKey pressedKey in keysToRemove)
		{
			_currentlyPressedKeys.Remove(pressedKey);
		}
	}

	private void AddKey(byte[] scancode)
	{
		if (_enableInterrupts && !IsServicingInterrupt)
		{
			INT = true;
		}

		foreach (byte b in scancode)
		{
			if (_buffer.Count < Constants.KeyboardScancodeQueueSize)
			{
				_buffer.Enqueue(b);
			}
			else
			{
				break;
			}
		}
	}

	private static byte[] KeyToScancode(KeyboardKey key)
	{
		return key switch
		{
			// Function keys
			KeyboardKey.F1 => [0x3B],
			KeyboardKey.F2 => [0x3C],
			KeyboardKey.F3 => [0x3D],
			KeyboardKey.F4 => [0x3E],
			KeyboardKey.F5 => [0x3F],
			KeyboardKey.F6 => [0x40],
			KeyboardKey.F7 => [0x41],
			KeyboardKey.F8 => [0x42],
			KeyboardKey.F9 => [0x43],
			KeyboardKey.F10 => [0x44],
			KeyboardKey.F11 => [0x57],
			KeyboardKey.F12 => [0x58],

			// Number row
			KeyboardKey.Grave => [0x29],
			KeyboardKey.One => [0x02],
			KeyboardKey.Two => [0x03],
			KeyboardKey.Three => [0x04],
			KeyboardKey.Four => [0x05],
			KeyboardKey.Five => [0x06],
			KeyboardKey.Six => [0x07],
			KeyboardKey.Seven => [0x08],
			KeyboardKey.Eight => [0x09],
			KeyboardKey.Nine => [0x0A],
			KeyboardKey.Zero => [0x0B],
			KeyboardKey.Minus => [0x0C],
			KeyboardKey.Equal => [0x0D],
			KeyboardKey.Backspace => [0x0E],

			// Top row
			KeyboardKey.Tab => [0x0F],
			KeyboardKey.Q => [0x10],
			KeyboardKey.W => [0x11],
			KeyboardKey.E => [0x12],
			KeyboardKey.R => [0x13],
			KeyboardKey.T => [0x14],
			KeyboardKey.Y => [0x15],
			KeyboardKey.U => [0x16],
			KeyboardKey.I => [0x17],
			KeyboardKey.O => [0x18],
			KeyboardKey.P => [0x19],
			KeyboardKey.LeftBracket => [0x1A],
			KeyboardKey.RightBracket => [0x1B],
			KeyboardKey.Backslash => [0x2B],

			// Home row
			KeyboardKey.CapsLock => [0x3A],
			KeyboardKey.A => [0x1E],
			KeyboardKey.S => [0x1F],
			KeyboardKey.D => [0x20],
			KeyboardKey.F => [0x21],
			KeyboardKey.G => [0x22],
			KeyboardKey.H => [0x23],
			KeyboardKey.J => [0x24],
			KeyboardKey.K => [0x25],
			KeyboardKey.L => [0x26],
			KeyboardKey.Semicolon => [0x27],
			KeyboardKey.Apostrophe => [0x28],
			KeyboardKey.Enter => [0x1C],

			// Bottom row
			KeyboardKey.LeftShift => [0x2A],
			KeyboardKey.Z => [0x2C],
			KeyboardKey.X => [0x2D],
			KeyboardKey.C => [0x2E],
			KeyboardKey.V => [0x2F],
			KeyboardKey.B => [0x30],
			KeyboardKey.N => [0x31],
			KeyboardKey.M => [0x32],
			KeyboardKey.Comma => [0x33],
			KeyboardKey.Period => [0x34],
			KeyboardKey.Slash => [0x35],
			KeyboardKey.RightShift => [0x36],

			// Modifiers / special
			KeyboardKey.LeftControl => [0x1D],
			KeyboardKey.LeftAlt => [0x38],
			KeyboardKey.Space => [0x39],
			KeyboardKey.RightAlt => [0xE0, 0x38],
			KeyboardKey.RightControl => [0xE0, 0x1D],

			// Navigation cluster
			KeyboardKey.Insert => [0xE0, 0x52],
			KeyboardKey.Home => [0xE0, 0x47],
			KeyboardKey.PageUp => [0xE0, 0x49],
			KeyboardKey.Delete => [0xE0, 0x53],
			KeyboardKey.End => [0xE0, 0x4F],
			KeyboardKey.PageDown => [0xE0, 0x51],

			// Arrow keys
			KeyboardKey.Up => [0xE0, 0x48],
			KeyboardKey.Left => [0xE0, 0x4B],
			KeyboardKey.Down => [0xE0, 0x50],
			KeyboardKey.Right => [0xE0, 0x4D],

			// Numpad
			KeyboardKey.NumLock => [0x45],
			KeyboardKey.KpDivide => [0xE0, 0x35],
			KeyboardKey.KpMultiply => [0x37],
			KeyboardKey.KpSubtract => [0x4A],
			KeyboardKey.KpAdd => [0x4E],
			KeyboardKey.KpEnter => [0xE0, 0x1C],
			KeyboardKey.KpDecimal => [0x53],
			KeyboardKey.Kp0 => [0x52],
			KeyboardKey.Kp1 => [0x4F],
			KeyboardKey.Kp2 => [0x50],
			KeyboardKey.Kp3 => [0x51],
			KeyboardKey.Kp4 => [0x4B],
			KeyboardKey.Kp5 => [0x4C],
			KeyboardKey.Kp6 => [0x4D],
			KeyboardKey.Kp7 => [0x47],
			KeyboardKey.Kp8 => [0x48],
			KeyboardKey.Kp9 => [0x49],

			// Misc
			KeyboardKey.Escape => [0x01],
			KeyboardKey.PrintScreen => [0xE0, 0x2A, 0xE0, 0x37], // Make only. Break sends alternate.
			KeyboardKey.ScrollLock => [0x46],
			KeyboardKey.Pause => [0xE1, 0x1D, 0x45, 0xE1, 0x9D, 0xC5], // No break code, long sequence

			_ => [],
		};
	}
}
