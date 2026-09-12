using Raylib_cs;

namespace IM800Emu;

// This will be made to read from an appsettings.json and allow reconfiguring at runtime later
// Hardcode defaults until I care
internal static class Config
{
	// -- System --
	public static Constants.DataSize DataBusWidth => Constants.DataSize.Word; // Default 16-bit data bus
	public static int CpuSpeedHz => 16 * 1000000; // Default 16 MHz
	public static int CpuMemorySizeBytes => 512 * 1024; // Default 512 KiB

	// -- Controller --
	public static KeyboardKey LeftKeyBinding => KeyboardKey.A;
	public static KeyboardKey RightKeyBinding => KeyboardKey.D;
	public static KeyboardKey UpKeyBinding => KeyboardKey.W;
	public static KeyboardKey DownKeyBinding => KeyboardKey.S;
	public static KeyboardKey AKeyBinding => KeyboardKey.L;
	public static KeyboardKey BKeyBinding => KeyboardKey.K;
	public static KeyboardKey XKeyBinding => KeyboardKey.O;
	public static KeyboardKey YKeyBinding => KeyboardKey.I;
	public static KeyboardKey LKeyBinding => KeyboardKey.U;
	public static KeyboardKey RKeyBinding => KeyboardKey.P;
	public static KeyboardKey StartKeyBinding => KeyboardKey.Enter;
	public static KeyboardKey SelectKeyBinding => KeyboardKey.RightShift;
	public static GamepadButton LeftGamepadBinding => GamepadButton.LeftFaceLeft;
	public static GamepadButton RightGamepadBinding => GamepadButton.LeftFaceRight;
	public static GamepadButton UpGamepadBinding => GamepadButton.LeftFaceUp;
	public static GamepadButton DownGamepadBinding => GamepadButton.LeftFaceDown;
	public static GamepadButton AGamepadBinding => GamepadButton.RightFaceRight;
	public static GamepadButton BGamepadBinding => GamepadButton.RightFaceDown;
	public static GamepadButton XGamepadBinding => GamepadButton.RightFaceUp;
	public static GamepadButton YGamepadBinding => GamepadButton.RightFaceLeft;
	public static GamepadButton LGamepadBinding => GamepadButton.LeftTrigger1;
	public static GamepadButton RGamepadBinding => GamepadButton.RightTrigger1;
	public static GamepadButton StartGamepadBinding => GamepadButton.MiddleRight;
	public static GamepadButton SelectGamepadBinding => GamepadButton.MiddleLeft;
}
