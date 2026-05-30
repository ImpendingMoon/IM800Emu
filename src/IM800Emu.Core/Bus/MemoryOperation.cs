namespace IM800Emu.Core.Bus;

/// <summary>
/// Represents a memory access. Includes the value read (if applicable) and
/// the number of cycles used to access this memory.
/// </summary>
public struct MemoryOperation
{
	public uint Data;
	public int Cycles;
}
