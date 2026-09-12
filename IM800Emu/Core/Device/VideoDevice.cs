using Raylib_cs;

namespace IM800Emu.Core.Device;

/// <summary>
///     Basic video device
///     Address map:
///     0 - Mode (Read/Write)
///     1 - Config (Read/Write)
///			- b0: Display Enable
/// </summary>
public class VideoDevice : IMemoryDevice, IInterruptingDevice
{
	private readonly RAMDevice _vram;
	private bool _enableDisplay;
	private Constants.VideoMode _currentMode;
	private Image _display;
	private Image _blankDisplay;

	public uint Length => 2;
	public bool INT { get; private set; }
	public bool NMI { get; private set; }
	public bool IsServicingInterrupt { get; private set; }


	private void TempDeleteMeFillVramWithGarbageToShowJunkOnScreenThanksBye()
	{
		uint length = _vram.Length;
		Random rng = new();

		for (uint i = 0; i < length; i++)
		{
			_vram.Write(i, Constants.DataSize.Byte, (uint)rng.Next(256));
		}

		_enableDisplay = true;
	}

	public VideoDevice(RAMDevice vram)
	{
		if (vram.Length < 65536)
		{
			throw new ArgumentException($"VRAM size must be >= 65536 bytes", nameof(vram));
		}

		_vram = vram;
		_enableDisplay = false;
		_currentMode = Constants.VideoMode.Bitmap320x200;
		_display = Raylib.GenImageColor(Constants.WindowWidth, Constants.WindowHeight, Color.Black);
		_blankDisplay = Raylib.GenImageColor(1, 1, Color.Black);

		TempDeleteMeFillVramWithGarbageToShowJunkOnScreenThanksBye();
	}

	~VideoDevice()
	{
		Raylib.UnloadImage(_display);
		Raylib.UnloadImage(_blankDisplay);
	}

	public uint Read(uint address, Constants.DataSize size)
	{
		address = address % Length;

		return address switch
		{
			0 => (uint)_currentMode,
			1 => (uint)(_enableDisplay ? 1 : 0),
			_ => 0,
		};
	}

	public void Write(uint address, Constants.DataSize size, uint value)
	{
		address = address % Length;

		switch (address)
		{
			case 0:
			{
				_currentMode = (Constants.VideoMode)value;
				break;
			}
			case 1:
			{
				if (value == 0)
				{
					_enableDisplay = false;
				}
				else if (value == 1)
				{
					_enableDisplay = true;
				}
				break;
			}
		}
	}

	public byte OnInterruptAcknowledge()
	{
		INT = false;
		IsServicingInterrupt = true;
		return (byte)Constants.VsyncInterruptNumber;
	}

	public void OnInterruptComplete()
	{
		IsServicingInterrupt = false;
	}

	public Image GetFrame()
	{
		if (!_enableDisplay)
		{
			return _blankDisplay;
		}

		switch (_currentMode)
		{
			case Constants.VideoMode.Bitmap320x200:
				RenderBitmap320x200Mode();
				break;
			default:
				throw new NotImplementedException($"Video mode {_currentMode} is unimplemented");
		}

		return _display;
	}

	/// <summary>
	/// Renders the contents of VRAM to the _display image using the 320x200x8bpp graphics mode.
	/// Note: This is a temporary method to get something on the screen.
	/// This device will be updated to draw the screen in step with the CPU so we can accurately emulate access timing
	/// and HSYNC/VSYNC interrupts.
	/// </summary>
	private void RenderBitmap320x200Mode()
	{
		const int width = 320;
		const int height = 200;

		if (_display.Width != width || _display.Height != height)
		{
			Raylib.UnloadImage(_display);
			_display = Raylib.GenImageColor(width, height, Color.Black);
		}

		// Linear row-major framebuffer starting at address 0
		for (int y = 0; y < height; y++)
		{
			for (int x = 0; x < width; x++)
			{
				uint address = (uint)((y * width) + x);
				byte index = (byte)_vram.Read(address, Constants.DataSize.Byte);

				Color color = GetColorFromPalette(index);

				Raylib.ImageDrawPixel(ref _display, x, y, color);
			}
		}
	}

	private Color GetColorFromPalette(byte index)
	{
		uint address = (uint)(Constants.RelativeColorPaletteAddress + (index * 4));
		uint rgb = _vram.Read(address, Constants.DataSize.Dword);

		// Colors are stored in RAM as [Red, Green, Blue, Unused], then read as little-endian dword
		// Break out components and turn them into a Raylib color
		byte red = (byte)((rgb >> 0) & 0xFF);
		byte green = (byte)((rgb >> 8) & 0xFF);
		byte blue = (byte)((rgb >> 16) & 0xFF);

		return new Color(red, green, blue);
	}
}
