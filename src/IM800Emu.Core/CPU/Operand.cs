namespace IM800Emu.Core.CPU;

public class Operand
{
	public Constants.DataSize DataSize { get; set; }
	public bool Indirect { get; set; }
	public Constants.RegisterTarget Register { get; set; }
	public uint Data { get; set; }
	public ushort Displacement { get; set; }
}
