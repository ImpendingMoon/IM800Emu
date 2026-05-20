using IM800Emu.Core.Bus;

namespace IM800Emu.Core.CPU;

/// <summary>
/// 
/// </summary>
public class IM800
{
	private Registers _registers;
	private MemoryBus _memoryBus;

	public IM800(MemoryBus memoryBus)
	{
		_registers = new();
		_memoryBus = memoryBus;
	}

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
		uint pc = _registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		var readResult = _memoryBus.Read(pc, Constants.DataSize.Word);

		return new();
	}

	/// <summary>
	/// Executes a decoded operation. Expects the operation to come from
	/// Decode, and may produce incorrect results when used with another
	/// operation.
	/// </summary>
	/// <param name="instruction"></param>
	/// <returns></returns>
	public int Execute(DecodedOperation instruction)
	{
		return 0;
	}

	private void DecodeFormatR(ref DecodedOperation decodedOperation)
	{

	}

	private void DecodeFormatRM(ref DecodedOperation decodedOperation)
	{

	}

	private void DecodeFormatUR(ref DecodedOperation decodedOperation)
	{

	}

	private void DecodeFormatUM(ref DecodedOperation decodedOperation)
	{

	}

	private void DecodeFormatB(ref DecodedOperation decodedOperation)
	{

	}

	private void DecodeFormatOneOff(ref DecodedOperation decodedOperation)
	{

	}

	private void DecodeFormatBlock(ref DecodedOperation decodedOperation)
	{

	}
}