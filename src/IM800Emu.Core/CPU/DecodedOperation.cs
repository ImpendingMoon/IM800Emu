namespace IM800Emu.Core.CPU;

/// <summary>
/// 
/// </summary>
public struct DecodedOperation
{
	public Constants.Operation Operation;
	public Operand? Destination;
	public Operand? Source;
	public int FetchCycles;
	public uint BaseAddress;
	public uint Size;

	public struct Operand
	{
		public Constants.DataSize Size;
		public Constants.RegisterTarget? Register;
		public uint? Value;
		public bool Indirect;
	}
}
