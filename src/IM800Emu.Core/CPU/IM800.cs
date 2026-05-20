namespace IM800Emu.Core.CPU;

/// <summary>
/// 
/// </summary>
public class IM800
{
	/// <summary>
	/// Decodes the next operation to execute. Includes interrupts, halt
	/// states, and the instruction at PC.
	/// </summary>
	/// <returns></returns>
	public DecodedOperation Decode()
	{
		return new();
	}

	/// <summary>
	/// Decodes the instruction at the given base address.
	/// </summary>
	/// <param name="baseAddress"></param>
	/// <returns></returns>
	public DecodedOperation DecodeAt(uint baseAddress)
	{
		return new();
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="instruction"></param>
	/// <returns></returns>
	public int Execute(DecodedOperation instruction)
	{
		return 0;
	}
}