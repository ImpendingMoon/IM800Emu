using IM800Emu.Core.Bus;

namespace IM800Emu.Core.CPU;

/// <summary>
/// Implements the 
/// </summary>
public partial class IM800
{
	private readonly Registers _registers;
	private readonly MemoryBus _memoryBus;

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
	public Result<DecodedOperation> Decode()
	{
		uint pc = _registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		return DecodeAt(pc);
	}

	/// <summary>
	/// 
	/// </summary>
	/// <param name="baseAddress"></param>
	/// <returns></returns>
	public Result<DecodedOperation> DecodeAt(uint baseAddress)
	{
		var result = new Result<DecodedOperation>()
		{
			IsSuccess = true,
			ResultObject = new()
			{
				Operation = Constants.Operation.Invalid,
				BaseAddress = baseAddress,
				Length = 2,
			},
		};

		Result<MemoryOperation> fetchResult = _memoryBus.Read(baseAddress, Constants.DataSize.Word);

		if (!fetchResult.IsSuccess)
		{
			result.IsSuccess = false;
			result.Exception = fetchResult.Exception;
			return result;
		}

		result.ResultObject.InstructionWord = (ushort)fetchResult.ResultObject.Data;
		result.ResultObject.FetchCycles = fetchResult.ResultObject.Cycles;

		byte groupSelector = (byte)(result.ResultObject.InstructionWord & 0b11);

		switch (groupSelector)
		{
			case 0b00:
			{
				DecodeFormatR(ref result);
				break;
			}
			case 0b01:
			{
				DecodeFormatRM(ref result);
				break;
			}
			case 0b10:
			{
				byte subgroupSelector = (byte)((result.ResultObject.InstructionWord >> 2) & 0b11);
				switch (subgroupSelector)
				{
					case 0b00:
					{
						DecodeFormatUR(ref result);
						break;
					}
					case 0b01:
					{
						DecodeFormatUM(ref result);
						break;
					}
					case 0b10:
					{
						DecodeFormatB(ref result);
						break;
					}
					case 0b11:
					{
						DecodeFormatM(ref result);
						break;
					}
				}
				break;
			}
			case 0b11:
			{
				byte subgroupSelector = (byte)((result.ResultObject.InstructionWord >> 2) & 0b11);
				switch (subgroupSelector)
				{
					case 0b00:
					{
						DecodeFormatSB(ref result);
						break;
					}
					case 0b01:
					{
						DecodeFormatBlock(ref result);
						break;
					}
					default:
					{
						result.IsSuccess = false;
						result.Exception = new InvalidOperationException(
									$"invalid special subgroup selector 0x{subgroupSelector:X2}"
								);
						break;
					}
				}
				break;
			}
		}

		return result;
	}

	/// <summary>
	/// Executes a decoded operation. Expects the operation to come from
	/// Decode, and may produce incorrect results when used with another
	/// operation.
	/// </summary>
	/// <param name="instruction"></param>
	/// <returns></returns>
	public int Execute(in DecodedOperation instruction)
	{
		return 0;
	}
}
