using IM800Emu.Core.Bus;
using System.Diagnostics;

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
		var decodeResult = new Result<DecodedOperation>()
		{
			ResultObject = new()
			{
				BaseAddress = baseAddress,
				Length = 2,
			},
		};

		Result<MemoryOperation> fetchResult = _memoryBus.Read(baseAddress, Constants.DataSize.Word);

		if (!fetchResult.IsSuccess)
		{
			decodeResult.Combine(fetchResult);
			return decodeResult;
		}

		Debug.Assert(fetchResult.ResultObject is not null);

		decodeResult.ResultObject.InstructionWord = (ushort)fetchResult.ResultObject.Data;
		decodeResult.ResultObject.FetchCycles = fetchResult.ResultObject.Cycles;

		byte groupSelector = (byte)(decodeResult.ResultObject.InstructionWord & 0b11);

		switch (groupSelector)
		{
			case 0b00:
			{
				DecodeFormatR(decodeResult);
				break;
			}
			case 0b01:
			{
				DecodeFormatRM(decodeResult);
				break;
			}
			case 0b10:
			{
				byte subgroupSelector = (byte)((decodeResult.ResultObject.InstructionWord >> 2) & 0b11);
				switch (subgroupSelector)
				{
					case 0b00:
					{
						DecodeFormatUR(decodeResult);
						break;
					}
					case 0b01:
					{
						DecodeFormatUM(decodeResult);
						break;
					}
					case 0b10:
					{
						DecodeFormatB(decodeResult);
						break;
					}
					case 0b11:
					{
						DecodeFormatM(decodeResult);
						break;
					}
				}
				break;
			}
			case 0b11:
			{
				byte subgroupSelector = (byte)((decodeResult.ResultObject.InstructionWord >> 2) & 0b11);
				switch (subgroupSelector)
				{
					case 0b00:
					{
						DecodeFormatSB(decodeResult);
						break;
					}
					case 0b01:
					{
						DecodeFormatBLK(decodeResult);
						break;
					}
					default:
					{
						decodeResult.AddError($"invalid special subgroup selector 0x{subgroupSelector:X2}");
						break;
					}
				}
				break;
			}
		}

		return decodeResult;
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
