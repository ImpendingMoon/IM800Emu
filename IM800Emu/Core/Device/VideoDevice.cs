using IM800Emu.Core.Machine;
using Raylib_cs;

namespace IM800Emu.Core.Device;

/// <summary>
///     Basic video device
///     Address map:
///     0 - Mode (Read/Write)
///     1 - Config (Read/Write)
///			- b0: Display Enable
/// </summary>
public class VideoDevice : IMemoryDevice, ISteppingDevice
{
	private readonly MachineContext _context;
	private readonly string _name;

	private readonly RAMDevice _vram;
	private readonly Color[] _cgaColorPalette = [
		new Color(0, 0, 0), // Black
		new Color(0, 0, 170), // Blue
		new Color(0, 170, 0), // Green
		new Color(0, 170, 170), // Cyan
		new Color(170, 0, 0), // Red
		new Color(170, 0, 170), // Magenta
		new Color(170, 85, 0), // Brown
		new Color(170, 170, 170), // Light Gray
		new Color(85, 85, 85), // Dark Gray
		new Color(85, 85, 255), // Bright Blue
		new Color(85, 255, 85), // Bright Green
		new Color(85, 255, 255), // Bright Cyan
		new Color(255, 85, 85), // Bright Red
		new Color(255, 85, 255), // Bright Magenta
		new Color(255, 255, 85), // Yellow
		new Color(255, 255, 255), // White
	];

	private Constants.VideoMode _currentMode;
	private bool _enableDisplay;
	private bool _enableCursor;
	private byte _cursorRow;
	private byte _cursorColumn;
	private int _cycleCount;
	private int _frameCount;

	private readonly Image _font;
	private const int _fontGlpyhWidth = 8;
	private const int _fontGlypthHeight = 8;
	private const int _fontCols = 32;

	private Image _display;
	private Image _blankDisplay;

	public uint Length => 4;
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
		_enableCursor = true;
	}

	public VideoDevice(RAMDevice vram, string name, MachineContext context)
	{
		if (vram.Length < 4096)
		{
			throw new ArgumentException($"VRAM size must be >= 4096 bytes", nameof(vram));
		}

		_context = context;
		_name = name;
		_vram = vram;

		_enableDisplay = false;
		_enableCursor = false;
		_cursorRow = 0;
		_cursorColumn = 0;
		_cycleCount = 0;
		_frameCount = 0;

		_currentMode = Constants.VideoMode.Text80x25;

		string fontPath = Path.Combine(AppContext.BaseDirectory, "Assets/font.png");
		_font = Raylib.LoadImage(fontPath);

		_display = Raylib.GenImageColor(640, 200, Color.Black);
		_blankDisplay = Raylib.GenImageColor(1, 1, Color.Black);

		TempDeleteMeFillVramWithGarbageToShowJunkOnScreenThanksBye();
	}

	~VideoDevice()
	{
		Raylib.UnloadImage(_font);
		Raylib.UnloadImage(_display);
		Raylib.UnloadImage(_blankDisplay);
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

		switch (address)
		{
			case 0:
			{
				value = (uint)_currentMode;
				break;
			}
			case 1:
			{
				value = (uint)(_enableDisplay ? 0b01 : 0);
				value |= (uint)(_enableCursor ? 0b10 : 0);
				break;
			}
			case 2:
			{
				value = _cursorRow;
				break;
			}
			case 3:
			{
				value = _cursorColumn;
				break;
			}
			default:
			{
				_context.Events.RaiseBeforeBadMemoryRead(address, size, _name);
				if (_context.CancelOperation)
				{
					return value;
				}
				break;
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

		switch (address)
		{
			case 0:
			{
				_currentMode = (Constants.VideoMode)value;
				break;
			}
			case 1:
			{
				bool enableDisplay = (value & 1) != 0;
				bool enableCursor = (value & 2) != 0;

				_enableDisplay = enableDisplay;
				_enableCursor = enableCursor;

				break;
			}
			case 2:
			{
				_cursorRow = (byte)value;
				break;
			}
			case 3:
			{
				_cursorColumn = (byte)value;
				break;
			}
			default:
			{
				_context.Events.RaiseBeforeBadMemoryWrite(address, size, value, _name);
				break;
			}
		}
	}

	public Image GetFrame()
	{
		if (!_enableDisplay)
		{
			return _blankDisplay;
		}

		switch (_currentMode)
		{
			case Constants.VideoMode.Text80x25:
				RenderText80x25Mode();
				break;
			default:
				Raylib.UnloadImage(_display);
				_display = Raylib.GenImageChecked(128, 128, 8, 8, Color.Magenta, Color.Black);
				break;
		}

		return _display;
	}

	private void RenderText80x25Mode()
	{
		const int rows = 25;
		const int cols = 80;
		const int width = cols * _fontGlpyhWidth;
		const int height = rows * _fontGlypthHeight;

		if (_display.Width != width || _display.Height != height)
		{
			Raylib.UnloadImage(_display);
			Raylib.GenImageColor(width, height, Color.Black);
		}

		for (int row = 0; row < rows; row++)
		{
			for (int col = 0; col < cols; col++)
			{
				int vram_index = (row * cols + col) * 2;

				var vram = _vram.AsSpan();

				byte character = vram[vram_index];
				byte attribute = vram[vram_index + 1];

				Color fg = _cgaColorPalette[attribute & 0x0F];
				Color bg = _cgaColorPalette[(attribute >> 4) & 0x0F];

				int glyphX = character % _fontCols * _fontGlpyhWidth;
				int glyphY = character / _fontCols * _fontGlypthHeight;

				Rectangle srcRec = new()
				{
					X = glyphX,
					Y = glyphY,
					Width = _fontGlpyhWidth,
					Height = _fontGlypthHeight,
				};

				int destX = col * _fontGlpyhWidth;
				int destY = row * _fontGlypthHeight;

				Rectangle destRec = new()
				{
					X = destX,
					Y = destY,
					Width = _fontGlpyhWidth,
					Height = _fontGlypthHeight,
				};

				// Draw background color
				Raylib.ImageDrawRectangleRec(ref _display, destRec, bg);
				// Draw glyph
				Raylib.ImageDraw(ref _display, _font, srcRec, destRec, fg);
			}
		}

		bool renderCursor = _enableCursor && _frameCount >= (Constants.TargetFramerate / 2);
		bool cursorInBounds = _cursorRow < rows && _cursorColumn < cols;

		if (renderCursor && cursorInBounds)
		{
			// Cursor as the bottom 2 rows of pixels in the cell
			int cursorX = _cursorColumn * _fontGlpyhWidth;
			int cursorY = _cursorRow * _fontGlypthHeight + _fontGlypthHeight - 2;

			Rectangle cursor = new()
			{
				X = cursorX,
				Y = cursorY,
				Width = _fontGlpyhWidth,
				Height = 2,
			};

			Color cursorColor = _cgaColorPalette[7]; // Light gray

			Raylib.ImageDrawRectangleRec(ref _display, cursor, cursorColor);
		}
	}

	public void Step(int cycles)
	{
		_cycleCount += cycles;

		if (_cycleCount >= _context.CyclesPerFrame)
		{
			_cycleCount -= _context.CyclesPerFrame;
			_frameCount++;
			if (_frameCount > Constants.TargetFramerate)
			{
				_frameCount = 0;
			}
		}
	}
}
