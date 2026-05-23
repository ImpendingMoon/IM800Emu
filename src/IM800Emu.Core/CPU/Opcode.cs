namespace IM800Emu.Core.CPU;

public struct Operand
{
	public Constants.DataSize DataSize;
	public bool Indirect;
	public Constants.RegisterTarget Register;
	// Immediate value or displacement
	public uint Data;
}
