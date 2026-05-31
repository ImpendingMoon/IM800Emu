namespace IM800Emu.Core.CPU;

/// <summary>
///
/// </summary>
public class DecodedOperation
{
	public Constants.Operation Operation { get; set; }
	public Operand? Destination { get; set; }
	public Operand? Source { get; set; }
	public int FetchCycles { get; set; }
	public uint BaseAddress { get; set; }
	public uint Length { get; set; }
	public ushort InstructionWord { get; set; }
	// Only LEA uses this field, but there's no particularly good way to get around having it in the instruction.
	public Constants.DataSize DataSize { get; set; }
	public Constants.Condition Condition { get; set; }
}
