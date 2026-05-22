using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;
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
	public Result<DecodedOperation> Decode()
	{
		uint pc = _registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		return new();
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
				Size = 2,
			},
		};


		var readResult = _memoryBus.Read(baseAddress, Constants.DataSize.Word);

		if (!readResult.IsSuccess)
		{
			result.IsSuccess = false;
			result.Exception = readResult.Exception;
			return result;
		}

		result.ResultObject.InstructionWord = (ushort)readResult.ResultObject.Data;
		result.ResultObject.FetchCycles = readResult.ResultObject.Cycles;

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
						byte miscSelector = (byte)((result.ResultObject.InstructionWord >> 4) & 0b1111);
						switch (miscSelector)
						{
							case 0b0000:
							{
								DecodeFormatOneOff(ref result);
								break;
							}
							case 0b0001:
							{
								DecodeFormatBlock(ref result);
								break;
							}
							default:
							{
								result.IsSuccess = false;
								result.Exception = new InvalidOperationException(
									$"invalid misc format selector 0x{miscSelector:X2}"
								);
								break;
							}
						}
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

	private void DecodeFormatR(ref Result<DecodedOperation> result)
	{

	}

	private void DecodeFormatRM(ref Result<DecodedOperation> result)
	{

	}

	private void DecodeFormatUR(ref Result<DecodedOperation> result)
	{

	}

	private void DecodeFormatUM(ref Result<DecodedOperation> result)
	{

	}

	private void DecodeFormatB(ref Result<DecodedOperation> result)
	{

	}

	private void DecodeFormatOneOff(ref Result<DecodedOperation> result)
	{

	}

	private void DecodeFormatBlock(ref Result<DecodedOperation> result)
	{

	}

	private void DecodeFormatSB(ref Result<DecodedOperation> result)
	{

	}

	/// <summary>
	/// Decodes a register target from a 3-bit selector
	/// </summary>
	/// <param name="selector"></param>
	/// <param name="size"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentException"></exception>
	private Constants.RegisterTarget DecodeRegister(byte selector, Constants.DataSize size)
	{
		if (size == Constants.DataSize.Byte || size == Constants.DataSize.Word)
		{
			return selector switch
			{
				0b000 => Constants.RegisterTarget.A,
				0b001 => Constants.RegisterTarget.B,
				0b010 => Constants.RegisterTarget.C,
				0b011 => Constants.RegisterTarget.D,
				0b100 => Constants.RegisterTarget.E,
				0b101 => Constants.RegisterTarget.H,
				0b110 => Constants.RegisterTarget.L,
				_ => throw new ArgumentException($"invalid register selector {selector:X}", nameof(selector)),
			};
		}
		else if (size == Constants.DataSize.Dword)
		{
			return selector switch
			{
				0b000 => Constants.RegisterTarget.AF,
				0b001 => Constants.RegisterTarget.BC,
				0b010 => Constants.RegisterTarget.DE,
				0b011 => Constants.RegisterTarget.HL,
				0b100 => Constants.RegisterTarget.IX,
				0b101 => Constants.RegisterTarget.IY,
				0b110 => Constants.RegisterTarget.SP,
				_ => throw new ArgumentException($"invalid register selector {selector:X}", nameof(selector)),
			};
		}
		throw new ArgumentException($"invalid register size {size}", nameof(size));
	}

	/// <summary>
	/// Decodes a size from a 2-bit selector
	/// </summary>
	/// <param name="selector"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentException"></exception>
	private Constants.DataSize DeocdeSize(byte selector)
	{
		return selector switch
		{
			0b00 => Constants.DataSize.Byte,
			0b01 => Constants.DataSize.Word,
			0b10 => Constants.DataSize.Dword,
			0b11 => Constants.DataSize.Qword,
			_ => throw new ArgumentException($"invliad size selector {selector:X}", nameof(selector)),
		};
	}
}
