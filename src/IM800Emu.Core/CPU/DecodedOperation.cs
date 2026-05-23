namespace IM800Emu.Core.CPU;

/// <summary>
///
/// </summary>
public struct DecodedOperation
{
	public Constants.Operation Operation;
	public Operand Destination;
	public Operand Source;
	public int FetchCycles;
	public uint BaseAddress;
	public uint Length;
	public ushort InstructionWord;
	public Constants.DataSize DataSize;
}
