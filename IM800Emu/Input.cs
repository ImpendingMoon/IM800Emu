using Raylib_cs;

namespace IM800Emu;

internal static class Input
{
	public enum ButtonBit
	{
		Left = 0b0000_0000_0001,
		Right = 0b0000_0000_0010,
		Up = 0b0000_0000_0100,
		Down = 0b0000_0000_1000,
		A = 0b0000_0001_0000,
		B = 0b0000_0010_0000,
		X = 0b0000_0100_0000,
		Y = 0b0000_1000_0000,
		L = 0b0001_0000_0000,
		R = 0b0010_0000_0000,
		Start = 0b0100_0000_0000,
		Select = 0b1000_0000_0000,
	}

	public static uint GetPressedButtonBitmap()
	{
		// Buttons short to ground when pressed
		uint pressed = 0xFFFFFFFF;
		int gamepad = 0;

		pressed &= ~GetPressedKeys();
		pressed &= ~GetPressedGamepadButtons(gamepad);

		return pressed;
	}

	private static uint GetPressedKeys()
	{
		uint pressed = 0;

		if (Raylib.IsKeyDown(Config.LeftKeyBinding))
		{
			pressed |= (uint)ButtonBit.Left;
		}

		if (Raylib.IsKeyDown(Config.RightKeyBinding))
		{
			pressed |= (uint)ButtonBit.Right;
		}

		if (Raylib.IsKeyDown(Config.UpKeyBinding))
		{
			pressed |= (uint)ButtonBit.Up;
		}

		if (Raylib.IsKeyDown(Config.DownKeyBinding))
		{
			pressed |= (uint)ButtonBit.Down;
		}

		if (Raylib.IsKeyDown(Config.AKeyBinding))
		{
			pressed |= (uint)ButtonBit.A;
		}

		if (Raylib.IsKeyDown(Config.BKeyBinding))
		{
			pressed |= (uint)ButtonBit.B;
		}

		if (Raylib.IsKeyDown(Config.XKeyBinding))
		{
			pressed |= (uint)ButtonBit.X;
		}

		if (Raylib.IsKeyDown(Config.YKeyBinding))
		{
			pressed |= (uint)ButtonBit.Y;
		}

		if (Raylib.IsKeyDown(Config.LKeyBinding))
		{
			pressed |= (uint)ButtonBit.L;
		}

		if (Raylib.IsKeyDown(Config.RKeyBinding))
		{
			pressed |= (uint)ButtonBit.R;
		}

		if (Raylib.IsKeyDown(Config.StartKeyBinding))
		{
			pressed |= (uint)ButtonBit.Start;
		}

		if (Raylib.IsKeyDown(Config.SelectKeyBinding))
		{
			pressed |= (uint)ButtonBit.Select;
		}

		return pressed;
	}

	private static uint GetPressedGamepadButtons(int gamepad)
	{
		uint pressed = 0;

		if (Raylib.IsGamepadAvailable(gamepad))
		{
			if (Raylib.IsGamepadButtonDown(gamepad, Config.LeftGamepadBinding))
			{
				pressed |= (uint)ButtonBit.Left;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.RightGamepadBinding))
			{
				pressed |= (uint)ButtonBit.Right;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.UpGamepadBinding))
			{
				pressed |= (uint)ButtonBit.Up;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.DownGamepadBinding))
			{
				pressed |= (uint)ButtonBit.Down;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.AGamepadBinding))
			{
				pressed |= (uint)ButtonBit.A;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.BGamepadBinding))
			{
				pressed |= (uint)ButtonBit.B;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.XGamepadBinding))
			{
				pressed |= (uint)ButtonBit.X;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.YGamepadBinding))
			{
				pressed |= (uint)ButtonBit.Y;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.LGamepadBinding))
			{
				pressed |= (uint)ButtonBit.L;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.RGamepadBinding))
			{
				pressed |= (uint)ButtonBit.R;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.StartGamepadBinding))
			{
				pressed |= (uint)ButtonBit.Start;
			}

			if (Raylib.IsGamepadButtonDown(gamepad, Config.SelectGamepadBinding))
			{
				pressed |= (uint)ButtonBit.Select;
			}
		}

		return pressed;
	}
}
