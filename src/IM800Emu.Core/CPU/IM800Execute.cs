using System.Diagnostics;
using IM800Emu.Core.Bus;

namespace IM800Emu.Core.CPU;

public partial class IM800
{
	private Result<int> ExecuteInvalid(DecodedOperation operation)
	{
		Result<int> result = new(operation.FetchCycles + 1);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	private Result<int> ExecuteHalted(DecodedOperation operation)
	{
		Result<int> result = new(4);
		return result;
	}

	private Result<int> ExecuteInterrupted(DecodedOperation operation)
	{
		Registers.SetFlag(Constants.FlagMask.EnableInterrupts, false);
		Registers.SetFlag(Constants.FlagMask.EnableInterruptsSave, false);

		byte interruptNumber = _interruptBus.AcknowledgeInterrupt();

		if (_interruptMode == 1)
		{
			interruptNumber = 1;
		}

		return InternalServiceInterrupt(interruptNumber);
	}

	private Result<int> ExecuteNonMaskableInterrupt(DecodedOperation operation)
	{
		_interruptBus.AcknowledgeNonMaskableInterrupt();

		bool enableInterrupts = Registers.GetFlag(Constants.FlagMask.EnableInterrupts);
		Registers.SetFlag(Constants.FlagMask.EnableInterruptsSave, enableInterrupts);
		Registers.SetFlag(Constants.FlagMask.EnableInterrupts, false);

		return InternalServiceInterrupt(2);
	}

	private Result<int> ExecuteLD(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		// Dest <- Source

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> writeDestResult = WriteOperand(
			operation.Destination,
			readSourceResult.ResultObject.Data
		);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteEX(DecodedOperation operation)
	{
		// Dest <-> Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		Result<MemoryOperation> writeSourceResult = WriteOperand(
			operation.Source,
			readDestResult.ResultObject.Data
		);
		result.Combine(writeSourceResult);
		result.ResultObject += writeSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> writeDestResult = WriteOperand(
			operation.Destination,
			readSourceResult.ResultObject.Data
		);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecutePUSH(DecodedOperation operation)
	{
		// sp <- sp - 4
		// [sp] <- dest

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Destination);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> pushResult = InternalPush(readSourceResult.ResultObject.Data);
		result.Combine(pushResult);
		result.ResultObject += pushResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecutePOP(DecodedOperation operation)
	{
		// dest <- [sp]
		// sp <- sp + 4

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> popResult = InternalPop();
		result.Combine(popResult);
		result.ResultObject += popResult.ResultObject.Cycles;

		Result<MemoryOperation> writeDestinationResult = WriteOperand(
			operation.Destination,
			popResult.ResultObject.Data
		);
		result.Combine(writeDestinationResult);
		result.ResultObject += writeDestinationResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteEXH(DecodedOperation operation)
	{
		// swap high and low halves of the operand

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readDestinationResult = ReadOperand(operation.Destination);
		result.Combine(readDestinationResult);
		result.ResultObject += readDestinationResult.ResultObject.Cycles;

		uint data = readDestinationResult.ResultObject.Data;
		switch (operation.Destination.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				uint temp = (data & 0xF) << 4;
				data = temp | ((data >> 4) & 0xF);
				break;
			}
			case Constants.DataSize.Word:
			{
				uint temp = (data & 0xFF) << 8;
				data = temp | ((data >> 8) & 0xFF);
				break;
			}
			case Constants.DataSize.Dword:
			{
				uint temp = (data & 0xFFFF) << 16;
				data = temp | ((data >> 16) & 0xFFFF);
				result.ResultObject += Constants.DwordALUCost;
				break;
			}
			default:
			{
				result.AddError("ExecuteEXH", $"invalid size for instruction EXH: {operation.Destination.DataSize}");
				break;
			}
		}

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteESA(DecodedOperation operation)
	{
		// extend, shift, add
		// dest <- dest + extend(source << scale)

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte shiftAmount = operation.DataSize switch
		{
			Constants.DataSize.Byte => 0,
			Constants.DataSize.Word => 1,
			Constants.DataSize.Dword => 2,
			Constants.DataSize.Qword => 3,
			_ => throw new InvalidOperationException($"impossible data size for instruction ESA: {operation.DataSize}"),
		};

		uint source = readSourceResult.ResultObject.Data;
		source = BitHelper.SignExtend(source, 16);
		source <<= shiftAmount;

		uint data = readDestResult.ResultObject.Data + source;

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteEXA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		if (operation.Destination.Register != default)
		{
			_registers.ExchangeWithAlternate(operation.Destination.Register, operation.Destination.DataSize);
		}
		else
		{
			result.AddError("ExecuteEXA", $"invalid operand for EXA: {operation.Destination}");
		}

		return result;
	}

	private Result<int> ExecuteEXX(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		_registers.ExchangeWithAlternate(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		_registers.ExchangeWithAlternate(Constants.RegisterTarget.DE, Constants.DataSize.Dword);
		_registers.ExchangeWithAlternate(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		return result;
	}

	private Result<int> ExecuteEXI(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		_registers.ExchangeWithAlternate(Constants.RegisterTarget.IX, Constants.DataSize.Dword);
		_registers.ExchangeWithAlternate(Constants.RegisterTarget.IY, Constants.DataSize.Dword);
		_registers.ExchangeWithAlternate(Constants.RegisterTarget.SP, Constants.DataSize.Dword);

		return result;
	}

	private Result<int> ExecuteIN_OUT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);
		Debug.Assert(operation.Destination.Indirect ^ operation.Source.Indirect);

		Result<int> result = new(operation.FetchCycles + 1);

		// OUT
		if (operation.Destination.Indirect)
		{
			Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
			result.Combine(readSourceResult);
			result.ResultObject += readSourceResult.ResultObject.Cycles;

			uint port = GetEffectiveAddress(operation.Destination);
			Result<MemoryOperation> writePortResult = _ioBus.Write(
				port,
				operation.DataSize,
				readSourceResult.ResultObject.Data
			);
			result.Combine(writePortResult);
			result.ResultObject += writePortResult.ResultObject.Cycles;
		}
		// IN
		else
		{
			uint port = GetEffectiveAddress(operation.Source);
			Result<MemoryOperation> readPortResult = _ioBus.Read(port, operation.DataSize);
			result.Combine(readPortResult);
			result.ResultObject += readPortResult.ResultObject.Cycles;

			Result<MemoryOperation> writeDestResult = WriteOperand(
				operation.Destination,
				readPortResult.ResultObject.Data
			);
			result.Combine(writeDestResult);
			result.ResultObject += writeDestResult.ResultObject.Cycles;
		}

		return result;
	}

	private Result<int> ExecuteADD(DecodedOperation operation)
	{
		// Dest <- Dest + Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = (byte)readSourceResult.ResultObject.Data;

				data = (byte)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = readSourceResult.ResultObject.Data;

				data = a + b;

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteADD", $"invalid size for instruction ADD: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteADC(DecodedOperation operation)
	{
		// Dest <- Dest + Source + Carry

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = (byte)readSourceResult.ResultObject.Data;
				if (Registers.GetFlag(Constants.FlagMask.Carry))
				{
					b++;
				}

				data = (byte)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = (ushort)readSourceResult.ResultObject.Data;
				if (Registers.GetFlag(Constants.FlagMask.Carry))
				{
					b++;
				}

				data = (ushort)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = readSourceResult.ResultObject.Data;
				if (Registers.GetFlag(Constants.FlagMask.Carry))
				{
					b++;
				}

				data = a + b;

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteADC", $"invalid size for instruction ADC: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;
		return result;
	}

	private Result<int> ExecuteSUB(DecodedOperation operation)
	{
		// Dest <- Dest - Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = (byte)readSourceResult.ResultObject.Data;

				data = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = readSourceResult.ResultObject.Data;

				data = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteSUB", $"invalid size for instruction SUB: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;
		return result;
	}

	private Result<int> ExecuteSBC(DecodedOperation operation)
	{
		// Dest <- Dest - Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = (byte)readSourceResult.ResultObject.Data;
				if (Registers.GetFlag(Constants.FlagMask.Carry))
				{
					b++;
				}

				data = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a - b);
				if (Registers.GetFlag(Constants.FlagMask.Carry))
				{
					b++;
				}

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = readSourceResult.ResultObject.Data;
				if (Registers.GetFlag(Constants.FlagMask.Carry))
				{
					b++;
				}

				data = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteSBC", $"invalid size for instruction SBC: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;
		return result;
	}

	private Result<int> ExecuteCP(DecodedOperation operation)
	{
		// Dest - Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = (byte)readSourceResult.ResultObject.Data;

				data = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = readSourceResult.ResultObject.Data;

				data = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteCP", $"invalid size for instruction CP: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		return result;
	}

	private Result<int> ExecuteINC(DecodedOperation operation)
	{
		// Dest <- Dest + 1

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = 1;

				data = (byte)(a + b);

				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = 1;

				data = (ushort)(a + b);

				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = 1;

				data = a + b;

				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteINC", $"invalid size for instruction INC: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteDEC(DecodedOperation operation)
	{
		// Dest <- Dest - 1

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = 1;

				data = (byte)(a - b);

				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = 1;

				data = (ushort)(a - b);

				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = 1;

				data = a - b;

				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteDEC", $"invalid size for instruction DEC: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;
		return result;
	}

	private Result<int> ExecuteNEG(DecodedOperation operation)
	{
		// Dest <- 0 - Dest

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = 0;
				byte b = (byte)readDestResult.ResultObject.Data;

				data = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = 0;
				ushort b = (ushort)readDestResult.ResultObject.Data;

				data = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = 0;
				uint b = readDestResult.ResultObject.Data;

				data = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteNEG", $"invalid size for instruction NEG: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;
		return result;
	}

	private Result<int> ExecuteEXT(DecodedOperation operation)
	{
		// Dest <- SignExtend(Dest, width / 2)

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				data = (byte)BitHelper.SignExtend(data, 4);
				break;
			}
			case Constants.DataSize.Word:
			{
				data = (ushort)BitHelper.SignExtend(data, 8);
				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				data = BitHelper.SignExtend(data, 16);
				break;
			}
			default:
			{
				result.AddError($"ExecuteEXT", $"invalid size for instruction EXT: {operation.DataSize}");
				break;
			}
		}

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;
		return result;
	}

	private Result<int> ExecuteMLT(DecodedOperation operation)
	{
		// Dest <- Dest.Hi * Dest.Lo

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
			{
				int multiplicand = (int)(data & 0xFF);
				int multiplier = (int)((data >> 8) & 0xFF);

				int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
				result.ResultObject += cyclesPerIteration * 8; // 8 Iterations for 8*8 mult
				result.ResultObject += BitHelper.NumberOfOnes((uint)multiplicand); // Roughly number of adds/subs needed

				data = (ushort)(multiplicand * multiplier);

				flagState.Carry = data > byte.MaxValue;
				flagState.Sign = (data & 0x8000) != 0;
				break;
			}
			case Constants.DataSize.Dword:
			{
				int multiplicand = (int)(data & 0xFFFF);
				int multiplier = (int)((data >> 16) & 0xFFFF);

				int cyclesPerIteration = 4 * Constants.DwordALUCost; // Branch, Shift, Loop Overhead
				result.ResultObject += cyclesPerIteration * 16; // 16 Iterations for 16*16 mult
				result.ResultObject += BitHelper.NumberOfOnes((uint)multiplicand); // Roughly number of adds/subs needed

				data = (ushort)(multiplicand * multiplier);

				flagState.Carry = data > byte.MaxValue;
				flagState.Sign = (data & 0x8000) != 0;
				break;
			}
			default:
			{
				result.AddError($"ExecuteMLT", $"invalid size for instruction MLT: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;
		return result;
	}

	private Result<int> ExecuteDIV(DecodedOperation operation)
	{
		// Quotient = Dest.Hi / Dest.Lo
		// Remainder = Dest.Hi % Dest.Lo
		// Dest.Hi <- Remainter
		// Dest.Lo <- Quotient

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
			{
				uint dividend = (data >> 8) & 0xFF;
				uint divisor = data & 0xFF;

				// Decision
				result.ResultObject += 1;
				if (divisor == 0)
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, true);
				}
				else
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, false);

					int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
					result.ResultObject += cyclesPerIteration * 8; // 8 Iterations for 8/8 div
					result.ResultObject += BitHelper.NumberOfOnes(dividend); // Roughly number of adds/subs needed

					data = (byte)(dividend / divisor); // Quotient in lower half
					data |= (uint)(byte)(dividend % divisor) << 8; // Remainder in upper half
				}
				break;
			}
			case Constants.DataSize.Dword:
			{
				uint dividend = (data >> 16) & 0xFFFF;
				uint divisor = data & 0xFFFF;

				// Decision
				result.ResultObject += 1;
				if (divisor == 0)
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, true);
				}
				else
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, false);

					int cyclesPerIteration = 4 * Constants.DwordALUCost; // Branch, Shift, Loop Overhead
					result.ResultObject += cyclesPerIteration * 16; // 16 Iterations for 16/16 div
					result.ResultObject += BitHelper.NumberOfOnes(dividend); // Roughly number of adds/subs needed

					data = (ushort)(dividend / divisor); // Quotient in lower half
					data |= (uint)(ushort)(dividend % divisor) << 16; // Remainder in upper half
				}
				break;
			}
			default:
			{
				result.AddError($"ExecuteDIV", $"invalid size for instruction DIV: {operation.DataSize}");
				break;
			}
		}

		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;
		return result;
	}

	private Result<int> ExecuteSDIV(DecodedOperation operation)
	{
		// Quotient = Dest.Hi / Dest.Lo
		// Remainder = Dest.Hi % Dest.Lo
		// Dest.Hi <- Remainter
		// Dest.Lo <- Quotient

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
			{
				int dividend = (int)((data >> 8) & 0xFF);
				int divisor = (int)(data & 0xFF);

				// Decision
				result.ResultObject += 1;
				if (divisor == 0)
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, true);
				}
				else
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, false);

					int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
					result.ResultObject += cyclesPerIteration * 8; // 8 Iterations for 8/8 div
					result.ResultObject += BitHelper.NumberOfOnes((uint)dividend); // Roughly number of adds/subs needed

					data = (byte)(dividend / divisor); // Quotient in lower half
					data |= (uint)(byte)(dividend % divisor) << 8; // Remainder in upper half
				}
				break;
			}
			case Constants.DataSize.Dword:
			{
				int dividend = (int)((data >> 16) & 0xFFFF);
				int divisor = (int)(data & 0xFFFF);

				// Decision
				result.ResultObject += 1;
				if (divisor == 0)
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, true);
				}
				else
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, false);

					int cyclesPerIteration = 4 * Constants.DwordALUCost; // Branch, Shift, Loop Overhead
					result.ResultObject += cyclesPerIteration * 16; // 16 Iterations for 16/16 div
					result.ResultObject += BitHelper.NumberOfOnes((uint)dividend); // Roughly number of adds/subs needed

					data = (ushort)(dividend / divisor); // Quotient in lower half
					data |= (uint)(ushort)(dividend % divisor) << 16; // Remainder in upper half
				}
				break;
			}
			default:
			{
				result.AddError($"ExecuteSDIV", $"invalid size for instruction SDIV: {operation.DataSize}");
				break;
			}
		}

		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;
		return result;
	}

	// Implementation taken from "The Undocumented Z80 Documented" by Sean Young
	private Result<int> ExecuteDAA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		uint a = _registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Byte);

		ALUFlagState flagState = GetALUFlags();

		int t = 0;

		if (flagState.HalfCarry || (a & 0xF) > 9)
		{
			t++;
		}

		if (flagState.Carry || a > 0x99)
		{
			t += 2;
			flagState.Carry = true;
		}

		if (flagState.Subtract && !flagState.HalfCarry)
		{
			flagState.HalfCarry = false;
		}
		else
		{
			if (flagState.Subtract && flagState.HalfCarry)
			{
				flagState.HalfCarry = (a & 0x0F) < 6;
			}
			else
			{
				flagState.HalfCarry = (a & 0x0F) >= 0x0A;
			}
		}

		switch (t)
		{
			case 1:
			{
				a += (byte)(flagState.Subtract ? 0xFA : 0x06); // -6:6
				break;
			}
			case 2:
			{
				a += (byte)(flagState.Subtract ? 0xA0 : 0x60); // -0x60:0x60
				break;
			}
			case 3:
			{
				a += (byte)(flagState.Subtract ? 0x9A : 0x66); // -0x66:0x66
				break;
			}
		}

		flagState.Sign = (a & 0x80) != 0;
		flagState.Zero = a == 0;
		flagState.ParityOverflow = BitHelper.IsParityEven(a);

		UpdateALUFlags(flagState);
		Registers.Write(Constants.RegisterTarget.A, Constants.DataSize.Byte, a);

		return result;
	}

	private Result<int> ExecuteAND(DecodedOperation operation)
	{
		// Dest <- Dest & Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = (byte)readSourceResult.ResultObject.Data;

				data = (byte)(a & b);

				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a & b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = readSourceResult.ResultObject.Data;

				data = a & b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteAND", $"invalid size for instruction AND: {operation.DataSize}");
				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteOR(DecodedOperation operation)
	{
		// Dest <- Dest | Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = (byte)readSourceResult.ResultObject.Data;

				data = (byte)(a | b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a | b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = readSourceResult.ResultObject.Data;

				data = a | b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteOR", $"invalid size for instruction OR: {operation.DataSize}");
				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteXOR(DecodedOperation operation)
	{
		// Dest <- Dest ^ Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = (byte)readSourceResult.ResultObject.Data;

				data = (byte)(a ^ b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a ^ b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = readSourceResult.ResultObject.Data;

				data = a ^ b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteXOR", $"invalid size for instruction XOR: {operation.DataSize}");
				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteTST(DecodedOperation operation)
	{
		// Dest & Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = (byte)readSourceResult.ResultObject.Data;

				data = (byte)(a & b);

				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a & b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = readSourceResult.ResultObject.Data;

				data = a & b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteTST", $"invalid size for instruction TST: {operation.DataSize}");
				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		return result;
	}

	private Result<int> ExecuteCPL(DecodedOperation operation)
	{
		// Dest <- Dest ^ -1

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.ResultObject.Data;
				byte b = 0xFF;

				data = (byte)(a ^ b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.ResultObject.Data;
				ushort b = 0xFFFF;

				data = (ushort)(a ^ b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = readDestResult.ResultObject.Data;
				uint b = 0xFFFFFFFF;

				data = a ^ b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteCPL", $"invalid size for instruction CPL: {operation.DataSize}");
				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteBIT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				break;
			}
			default:
			{
				result.AddError($"ExecuteBIT", $"invalid size for instruction BIT: {operation.DataSize}");
				break;
			}
		}

		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = ((readDestResult.ResultObject.Data >> bit) & 1) == 0;
		UpdateALUFlags(flagState);

		return result;
	}

	private Result<int> ExecuteSET(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		uint data = readDestResult.ResultObject.Data;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				break;
			}
			default:
			{
				result.AddError($"ExecuteSET", $"invalid size for instruction SET: {operation.DataSize}");
				break;
			}
		}

		data |= (uint)(1 << bit);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteRES(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		uint data = readDestResult.ResultObject.Data;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				break;
			}
			default:
			{
				result.AddError($"ExecuteRES", $"invalid size for instruction RES: {operation.DataSize}");
				break;
			}
		}

		data &= ~(uint)(1 << bit);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteRLC(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)data;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit7 = (byte)(a & 0x80);
					a = (byte)((a << 1) | (bit7 >> 7));
					flagState.Carry = bit7 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x80) != 0;
				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)data;

				for (int i = 0; i < bit; i++)
				{
					ushort bit15 = (ushort)(a & 0x8000);
					a = (ushort)((a << 1) | (bit15 >> 15));
					flagState.Carry = bit15 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				uint a = data;

				for (int i = 0; i < bit; i++)
				{
					uint bit31 = a & 0x80000000;
					a = (a << 1) | (bit31 >> 31);
					flagState.Carry = bit31 != 0;
					result.ResultObject += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteRLC", $"invalid size for instruction RLC: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteRRC(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)data;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit0 = (byte)(a & 1);
					a = (byte)((a >> 1) | (bit0 << 7));
					flagState.Carry = bit0 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)data;

				for (int i = 0; i < bit; i++)
				{
					ushort bit0 = (ushort)(a & 1);
					a = (ushort)((a >> 1) | (bit0 << 15));
					flagState.Carry = bit0 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				uint a = data;

				for (int i = 0; i < bit; i++)
				{
					uint bit0 = a & 1;
					a = (a >> 1) | (bit0 << 31);
					flagState.Carry = bit0 != 0;
					result.ResultObject += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteRRC", $"invalid size for instruction RRC: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteRL(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)data;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit7 = (byte)(a & 0x80);
					a = (byte)(a << 1);
					a |= (byte)(flagState.Carry ? 1 : 0);
					flagState.Carry = bit7 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)data;

				for (int i = 0; i < bit; i++)
				{
					ushort bit15 = (ushort)(a & 0x8000);
					a = (ushort)(a << 1);
					a |= (ushort)(flagState.Carry ? 1 : 0);
					flagState.Carry = bit15 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				uint a = data;

				for (int i = 0; i < bit; i++)
				{
					uint bit31 = a & 0x80000000;
					a = a << 1;
					a |= (uint)(flagState.Carry ? 1 : 0);
					flagState.Carry = bit31 != 0;
					result.ResultObject += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteRL", $"invalid size for instruction RL: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteRR(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)data;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit0 = (byte)(a & 1);
					a = (byte)((a >> 1) | ((flagState.Carry ? 1 : 0) << 7));
					flagState.Carry = bit0 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)data;

				for (int i = 0; i < bit; i++)
				{
					ushort bit0 = (ushort)(a & 1);
					a = (ushort)((a >> 1) | ((flagState.Carry ? 1 : 0) << 15));
					flagState.Carry = bit0 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				uint a = data;

				for (int i = 0; i < bit; i++)
				{
					uint bit0 = a & 1;
					a = (a >> 1) | ((uint)(flagState.Carry ? 1 : 0) << 31);
					flagState.Carry = bit0 != 0;
					result.ResultObject += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteRR", $"invalid size for instruction RR: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteSLA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)data;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit7 = (byte)(a & 0x80);
					a = (byte)(a << 1);
					flagState.Carry = bit7 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)data;

				for (int i = 0; i < bit; i++)
				{
					ushort bit15 = (ushort)(a & 0x8000);
					a = (ushort)(a << 1);
					flagState.Carry = bit15 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				uint a = data;

				for (int i = 0; i < bit; i++)
				{
					uint bit31 = a & 0x80000000;
					a = a << 1;
					flagState.Carry = bit31 != 0;
					result.ResultObject += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteSLA", $"invalid size for instruction SLA: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteSRA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)data;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit0 = (byte)(a & 1);
					a = (byte)(a >> 1);
					flagState.Carry = bit0 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)data;

				for (int i = 0; i < bit; i++)
				{
					ushort bit0 = (ushort)(a & 1);
					a = (ushort)(a >> 1);
					flagState.Carry = bit0 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				uint a = data;

				for (int i = 0; i < bit; i++)
				{
					uint bit0 = a & 1;
					a >>= 1;
					flagState.Carry = bit0 != 0;
					result.ResultObject += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteSRA", $"invalid size for instruction SRA: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteSRL(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<MemoryOperation> readSourceResult = ReadOperand(operation.Source);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> readDestResult = ReadOperand(operation.Destination);
		result.Combine(readDestResult);
		result.ResultObject += readDestResult.ResultObject.Cycles;

		byte bit = (byte)readSourceResult.ResultObject.Data;
		uint data = readDestResult.ResultObject.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				bit &= 0b111;
				byte a = (byte)data;

				// Microcoded multi-bit shift
				for (int i = 0; i < bit; i++)
				{
					byte bit0 = (byte)(a & 1);
					a = (byte)(a >>> 1);
					flagState.Carry = bit0 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				bit &= 0b1111;
				ushort a = (ushort)data;

				for (int i = 0; i < bit; i++)
				{
					ushort bit0 = (ushort)(a & 1);
					a = (ushort)(a >>> 1);
					flagState.Carry = bit0 != 0;
					result.ResultObject++;
				}

				data = a;
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				bit &= 0b11111;
				uint a = data;

				for (int i = 0; i < bit; i++)
				{
					uint bit0 = a & 1;
					a >>>= 1;
					flagState.Carry = bit0 != 0;
					result.ResultObject += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"ExecuteSRL", $"invalid size for instruction SRL: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		UpdateALUFlags(flagState);

		Result<MemoryOperation> writeDestResult = WriteOperand(operation.Destination, data);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		return result;
	}

	private Result<int> ExecuteRLD(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);
		Result<MemoryOperation> readHLResult = _memoryBus.Read(hl, Constants.DataSize.Byte);
		result.Combine(readHLResult);
		result.ResultObject += readHLResult.ResultObject.Cycles;

		byte a = (byte)Registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Byte);
		byte mem = (byte)readHLResult.ResultObject.Data;

		byte aLow = (byte)(a & 0x0F);
		byte memHigh = (byte)((mem >> 4) & 0x0F);
		byte memLow = (byte)(mem & 0x0F);

		byte newA = (byte)((a & 0xF0) | memHigh);
		byte newMem = (byte)((memLow << 4) | aLow);

		Result<MemoryOperation> writeHLResult = _memoryBus.Write(hl, Constants.DataSize.Byte, newMem);
		result.ResultObject += writeHLResult.ResultObject.Cycles;

		ALUFlagState flagState = GetALUFlags();
		flagState.Sign = (newA & 0x80) != 0;
		flagState.Zero = newA == 0;
		flagState.ParityOverflow = BitHelper.IsParityEven(newA);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		UpdateALUFlags(flagState);

		Registers.Write(Constants.RegisterTarget.A, Constants.DataSize.Byte, newA);

		result.ResultObject += 2;
		return result;
	}

	private Result<int> ExecuteRRD(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);
		Result<MemoryOperation> readHLResult = _memoryBus.Read(hl, Constants.DataSize.Byte);
		result.Combine(readHLResult);
		result.ResultObject += readHLResult.ResultObject.Cycles;

		byte a = (byte)Registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Byte);
		byte mem = (byte)readHLResult.ResultObject.Data;

		byte aLow = (byte)(a & 0x0F);
		byte memHigh = (byte)((mem >> 4) & 0x0F);
		byte memLow = (byte)(mem & 0x0F);

		byte newA = (byte)((a & 0xF0) | memLow);
		byte newMem = (byte)((aLow << 4) | memHigh);

		Result<MemoryOperation> writeHLResult = _memoryBus.Write(hl, Constants.DataSize.Byte, newMem);
		result.ResultObject += writeHLResult.ResultObject.Cycles;

		ALUFlagState flagState = GetALUFlags();
		flagState.Sign = (newA & 0x80) != 0;
		flagState.Zero = newA == 0;
		flagState.ParityOverflow = BitHelper.IsParityEven(newA);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		UpdateALUFlags(flagState);

		Registers.Write(Constants.RegisterTarget.A, Constants.DataSize.Byte, newA);

		result.ResultObject += 2;
		return result;
	}

	private Result<int> ExecuteNOP(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);
		Result<int> result = new(operation.FetchCycles + 1);
		return result;
	}

	private Result<int> ExecuteJP(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		if (IsConditionTrue(operation.Condition))
		{
			Result<MemoryOperation> addressReadResult = ReadOperand(operation.Destination);
			result.Combine(addressReadResult);
			result.ResultObject += addressReadResult.ResultObject.Cycles;

			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, addressReadResult.ResultObject.Data);
		}

		return result;
	}

	private Result<int> ExecuteJR(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		if (IsConditionTrue(operation.Condition))
		{
			Result<MemoryOperation> addressReadResult = ReadOperand(operation.Destination);
			result.Combine(addressReadResult);
			result.ResultObject += addressReadResult.ResultObject.Cycles;

			int displacement = 0;

			if (operation.DataSize == Constants.DataSize.Byte)
			{
				displacement = (int)BitHelper.SignExtend(addressReadResult.ResultObject.Data, 8);
			}
			else if (operation.DataSize == Constants.DataSize.Word)
			{
				displacement = (int)BitHelper.SignExtend(addressReadResult.ResultObject.Data, 16);
			}
			else
			{
				result.AddError("ExecuteJR", $"invalid size for instruction JR: {operation.DataSize}");
			}

			int pc = (int)Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			pc += displacement;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, (uint)pc);
		}

		return result;
	}

	private Result<int> ExecuteDJNZ(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		ushort b = (ushort)_registers.Read(Constants.RegisterTarget.B, Constants.DataSize.Word);
		b--;
		_registers.Write(Constants.RegisterTarget.B, Constants.DataSize.Word, b);

		if (b != 0)
		{
			int pc = (int)_registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			int displacement = (int)BitHelper.SignExtend(operation.Destination.Data, 8);
			pc += displacement;
			_registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, (uint)pc);
			result.ResultObject += 2;
		}

		return result;
	}

	private Result<int> ExecuteJAZ(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		ushort a = (ushort)_registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Word);

		if (a == 0)
		{
			int pc = (int)_registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			int displacement = (int)BitHelper.SignExtend(operation.Destination.Data, 8);
			pc += displacement;
			_registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, (uint)pc);
			result.ResultObject += 2;
		}

		return result;
	}

	private Result<int> ExecuteJANZ(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		ushort a = (ushort)_registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Word);

		if (a != 0)
		{
			int pc = (int)_registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			int displacement = (int)BitHelper.SignExtend(operation.Destination.Data, 8);
			pc += displacement;
			_registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, (uint)pc);
			result.ResultObject += 2;
		}

		return result;
	}

	private Result<int> ExecuteCALL(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		if (IsConditionTrue(operation.Condition))
		{
			uint pc = _registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			Result<MemoryOperation> pushResult = InternalPush(pc);
			result.Combine(pushResult);
			result.ResultObject += pushResult.ResultObject.Cycles;

			Result<MemoryOperation> addressReadResult = ReadOperand(operation.Destination);
			result.Combine(addressReadResult);
			result.ResultObject += addressReadResult.ResultObject.Cycles;

			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, addressReadResult.ResultObject.Data);
		}

		return result;
	}

	private Result<int> ExecuteCR(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		if (IsConditionTrue(operation.Condition))
		{
			uint pc = _registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			Result<MemoryOperation> pushResult = InternalPush(pc);
			result.Combine(pushResult);
			result.ResultObject += pushResult.ResultObject.Cycles;

			Result<MemoryOperation> addressReadResult = ReadOperand(operation.Destination);
			result.Combine(addressReadResult);
			result.ResultObject += addressReadResult.ResultObject.Cycles;

			int displacement = 0;

			if (operation.DataSize == Constants.DataSize.Byte)
			{
				displacement = (int)BitHelper.SignExtend(addressReadResult.ResultObject.Data, 8);
			}
			else if (operation.DataSize == Constants.DataSize.Word)
			{
				displacement = (int)BitHelper.SignExtend(addressReadResult.ResultObject.Data, 16);
			}
			else
			{
				result.AddError("ExecuteJR", $"invalid size for instruction JR: {operation.DataSize}");
			}

			pc = (uint)((int)pc + displacement);
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, (uint)pc);
		}

		return result;
	}

	private Result<int> ExecuteRET(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		if (IsConditionTrue(operation.Condition))
		{
			Result<MemoryOperation> popResult = InternalPop();
			result.Combine(popResult);
			result.ResultObject += popResult.ResultObject.Cycles;
			_registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, popResult.ResultObject.Data);
		}

		return result;
	}

	private Result<int> ExecuteRETI(DecodedOperation operation)
	{
		Result<int> result = new(operation.FetchCycles + 1);

		_interruptBus.CompleteInterrupt();

		Result<MemoryOperation> popResult = InternalPop();
		result.Combine(popResult);
		result.ResultObject += popResult.ResultObject.Cycles;
		_registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, popResult.ResultObject.Data);

		return result;
	}

	private Result<int> ExecuteRETN(DecodedOperation operation)
	{
		Result<int> result = new(operation.FetchCycles + 1);

		bool enableInterrupts = _registers.GetFlag(Constants.FlagMask.EnableInterruptsSave);
		_registers.SetFlag(Constants.FlagMask.EnableInterrupts, enableInterrupts);

		Result<MemoryOperation> popResult = InternalPop();
		result.Combine(popResult);
		result.ResultObject += popResult.ResultObject.Cycles;
		_registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, popResult.ResultObject.Data);

		return result;
	}

	private Result<int> ExecuteRST(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Result<int> serviceInterruptResult = InternalServiceInterrupt((byte)operation.Destination.Data);
		result.Combine(serviceInterruptResult);
		result.ResultObject += serviceInterruptResult.ResultObject;

		return result;
	}

	private Result<int> ExecuteSCF(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		_registers.SetFlag(Constants.FlagMask.Carry, true);

		return result;
	}

	private Result<int> ExecuteCCF(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		bool carry = _registers.GetFlag(Constants.FlagMask.Carry);
		_registers.SetFlag(Constants.FlagMask.Carry, !carry);

		return result;
	}

	private Result<int> ExecuteEI(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		_pendingEnableInterrupts = true;

		return result;
	}

	private Result<int> ExecuteDI(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		_registers.SetFlag(Constants.FlagMask.EnableInterrupts, false);

		return result;
	}

	private Result<int> ExecuteIM1(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		_interruptMode = 1;

		return result;
	}

	private Result<int> ExecuteIM2(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		_interruptMode = 2;

		return result;
	}

	private Result<int> ExecuteHALT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		_halted = true;

		return result;
	}

	private Result<int> ExecuteLDI(DecodedOperation operation)
	{
		// decode only grabs the immediate in destination
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		// I is only 22 bits long (shifted left 10 to form upper bits of IVT address)
		uint newI = operation.Destination.Data & 0b1111111111111111111111;
		_registers.Write(Constants.RegisterTarget.I, Constants.DataSize.Dword, newI);

		return result;
	}

	private Result<int> ExecuteLDAR(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		uint r = _registers.Read(Constants.RegisterTarget.R, Constants.DataSize.Word);
		_registers.Write(Constants.RegisterTarget.A, Constants.DataSize.Word, r);

		return result;
	}

	private Result<int> ExecuteLDRA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		uint a = _registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Word);
		_registers.Write(Constants.RegisterTarget.R, Constants.DataSize.Word, a);

		return result;
	}

	private Result<int> ExecuteBLD(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		uint bc = Registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		uint de = Registers.Read(Constants.RegisterTarget.DE, Constants.DataSize.Dword);
		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		Result<MemoryOperation> readSourceResult = _memoryBus.Read(hl, operation.DataSize);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		Result<MemoryOperation> writeDestResult = _memoryBus.Write(
			de,
			operation.DataSize,
			readSourceResult.ResultObject.Data
		);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		uint adjustAmount = 0;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				adjustAmount = 1;
				break;
			}
			case Constants.DataSize.Word:
			{
				adjustAmount = 2;
				break;
			}
			case Constants.DataSize.Dword:
			{
				adjustAmount = 4;
				break;
			}
			default:
			{
				result.AddError("ExecuteBLD", $"invalid size for BLD: {operation.DataSize}");
				break;
			}
		}

		// Increment/Decrement pointers
		if (operation.Increment)
		{
			hl += adjustAmount;
			de += adjustAmount;
		}
		else
		{
			hl -= adjustAmount;
			de -= adjustAmount;
		}

		// Decrement counter and update flags
		bc--;

		ALUFlagState flagState = GetALUFlags();
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.ParityOverflow = bc != 0;
		UpdateALUFlags(flagState);

		// Writeback registers
		Registers.Write(Constants.RegisterTarget.BC, Constants.DataSize.Dword, bc);
		Registers.Write(Constants.RegisterTarget.DE, Constants.DataSize.Dword, de);
		Registers.Write(Constants.RegisterTarget.HL, Constants.DataSize.Dword, hl);

		// if Parity/Overflow is true (BC != 0), continue
		if (operation.Repeat && Registers.GetFlag(Constants.FlagMask.ParityOverflow))
		{
			// Undo PC increment
			uint pc = Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			pc -= operation.Length;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, pc);

			// Set up to repeat
			if (_currentBlockOperation is null)
			{
				// Further fetches cost nothing
				operation.FetchCycles = 0;
				_currentBlockOperation = operation;
			}
		}
		// Done
		else
		{
			_currentBlockOperation = null;
		}

		return result;
	}

	private Result<int> ExecuteBCP(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Constants.RegisterTarget afRegisterTarget;
		if (operation.DataSize == Constants.DataSize.Dword)
		{
			afRegisterTarget = Constants.RegisterTarget.AF;
		}
		else
		{
			afRegisterTarget = Constants.RegisterTarget.A;
		}

		uint af = Registers.Read(afRegisterTarget, operation.DataSize);
		uint bc = Registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		Result<MemoryOperation> readSourceResult = _memoryBus.Read(hl, operation.DataSize);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		uint data = af;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)af;
				byte b = (byte)readSourceResult.ResultObject.Data;

				data = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)af;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = af;
				uint b = readSourceResult.ResultObject.Data;

				data = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"InternalCompare", $"invalid size for compare: {operation.DataSize}");
				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;

		uint adjustAmount = 0;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				adjustAmount = 1;
				break;
			}
			case Constants.DataSize.Word:
			{
				adjustAmount = 2;
				break;
			}
			case Constants.DataSize.Dword:
			{
				adjustAmount = 4;
				break;
			}
			default:
			{
				result.AddError("ExecuteBCP", $"invalid size for BCP: {operation.DataSize}");
				break;
			}
		}

		// Increment/Decrement pointers
		if (operation.Increment)
		{
			hl += adjustAmount;
		}
		else
		{
			hl -= adjustAmount;
		}

		// Decrement counter and update flags
		bc--;

		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.ParityOverflow = bc != 0;
		UpdateALUFlags(flagState);

		// Writeback registers
		Registers.Write(Constants.RegisterTarget.BC, Constants.DataSize.Dword, bc);
		Registers.Write(Constants.RegisterTarget.HL, Constants.DataSize.Dword, hl);

		// if not Zero and Parity/Overflow is true (BC != 0), continue
		if (!flagState.Zero && operation.Repeat && Registers.GetFlag(Constants.FlagMask.ParityOverflow))
		{
			// Undo PC increment
			uint pc = Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			pc -= operation.Length;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, pc);

			// Set up to repeat
			if (_currentBlockOperation is null)
			{
				// Further fetches cost nothing
				operation.FetchCycles = 0;
				_currentBlockOperation = operation;
			}
		}
		// Done
		else
		{
			_currentBlockOperation = null;
		}

		return result;
	}

	private Result<int> ExecuteBTST(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		Constants.RegisterTarget afRegisterTarget;
		if (operation.DataSize == Constants.DataSize.Dword)
		{
			afRegisterTarget = Constants.RegisterTarget.AF;
		}
		else
		{
			afRegisterTarget = Constants.RegisterTarget.A;
		}

		uint af = Registers.Read(afRegisterTarget, operation.DataSize);
		uint bc = Registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		Result<MemoryOperation> readSourceResult = _memoryBus.Read(hl, operation.DataSize);
		result.Combine(readSourceResult);
		result.ResultObject += readSourceResult.ResultObject.Cycles;

		uint data = af;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)af;
				byte b = (byte)readSourceResult.ResultObject.Data;

				data = (byte)(a & b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)af;
				ushort b = (ushort)readSourceResult.ResultObject.Data;

				data = (ushort)(a & b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				result.ResultObject += Constants.DwordALUCost;

				uint a = af;
				uint b = readSourceResult.ResultObject.Data;

				data = a & b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				result.AddError($"BTST", $"invalid size for BTST: {operation.DataSize}");
				break;
			}
		}

		flagState.Carry = false;
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = data == 0;

		uint adjustAmount = 0;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				adjustAmount = 1;
				break;
			}
			case Constants.DataSize.Word:
			{
				adjustAmount = 2;
				break;
			}
			case Constants.DataSize.Dword:
			{
				adjustAmount = 4;
				break;
			}
			default:
			{
				result.AddError("ExecuteBCP", $"invalid size for BCP: {operation.DataSize}");
				break;
			}
		}

		// Increment/Decrement pointers
		if (operation.Increment)
		{
			hl += adjustAmount;
		}
		else
		{
			hl -= adjustAmount;
		}

		// Decrement counter and update flags
		bc--;

		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.ParityOverflow = bc != 0;
		UpdateALUFlags(flagState);

		// Writeback registers
		Registers.Write(Constants.RegisterTarget.BC, Constants.DataSize.Dword, bc);
		Registers.Write(Constants.RegisterTarget.HL, Constants.DataSize.Dword, hl);

		// if not Zero and Parity/Overflow is true (BC != 0), continue
		if (!flagState.Zero && operation.Repeat && Registers.GetFlag(Constants.FlagMask.ParityOverflow))
		{
			// Undo PC increment
			uint pc = Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			pc -= operation.Length;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, pc);

			// Set up to repeat
			if (_currentBlockOperation is null)
			{
				// Further fetches cost nothing
				operation.FetchCycles = 0;
				_currentBlockOperation = operation;
			}
		}
		// Done
		else
		{
			_currentBlockOperation = null;
		}

		return result;
	}

	private Result<int> ExecuteBIN(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		uint bc = Registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		uint de = Registers.Read(Constants.RegisterTarget.DE, Constants.DataSize.Dword);
		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		Result<MemoryOperation> readPortResult = _ioBus.Read(hl, operation.DataSize);
		result.Combine(readPortResult);
		result.ResultObject += readPortResult.ResultObject.Cycles;

		Result<MemoryOperation> writeDestResult = _memoryBus.Write(
			de,
			operation.DataSize,
			readPortResult.ResultObject.Data
		);
		result.Combine(writeDestResult);
		result.ResultObject += writeDestResult.ResultObject.Cycles;

		uint adjustAmount = 0;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				adjustAmount = 1;
				break;
			}
			case Constants.DataSize.Word:
			{
				adjustAmount = 2;
				break;
			}
			case Constants.DataSize.Dword:
			{
				adjustAmount = 4;
				break;
			}
			default:
			{
				result.AddError("ExecuteBLD", $"invalid size for BLD: {operation.DataSize}");
				break;
			}
		}

		// Increment/Decrement memory pointer (not port)
		if (operation.Increment)
		{
			de += adjustAmount;
		}
		else
		{
			de -= adjustAmount;
		}

		// Decrement counter and update flags
		bc--;

		ALUFlagState flagState = GetALUFlags();
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.ParityOverflow = bc != 0;
		UpdateALUFlags(flagState);

		// Writeback registers
		Registers.Write(Constants.RegisterTarget.BC, Constants.DataSize.Dword, bc);
		Registers.Write(Constants.RegisterTarget.DE, Constants.DataSize.Dword, de);
		Registers.Write(Constants.RegisterTarget.HL, Constants.DataSize.Dword, hl);

		// if Parity/Overflow is true (BC != 0), continue
		if (operation.Repeat && Registers.GetFlag(Constants.FlagMask.ParityOverflow))
		{
			// Undo PC increment
			uint pc = Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			pc -= operation.Length;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, pc);

			// Set up to repeat
			if (_currentBlockOperation is null)
			{
				// Further fetches cost nothing
				operation.FetchCycles = 0;
				_currentBlockOperation = operation;
			}
		}
		// Done
		else
		{
			_currentBlockOperation = null;
		}

		return result;
	}

	private Result<int> ExecuteBOUT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		uint bc = Registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		uint de = Registers.Read(Constants.RegisterTarget.DE, Constants.DataSize.Dword);
		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		Result<MemoryOperation> readPortResult = _ioBus.Read(hl, operation.DataSize);
		result.Combine(readPortResult);
		result.ResultObject += readPortResult.ResultObject.Cycles;

		Result<MemoryOperation> writePortResult = _ioBus.Write(
			de,
			operation.DataSize,
			readPortResult.ResultObject.Data
		);
		result.Combine(writePortResult);
		result.ResultObject += writePortResult.ResultObject.Cycles;

		uint adjustAmount = 0;

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				adjustAmount = 1;
				break;
			}
			case Constants.DataSize.Word:
			{
				adjustAmount = 2;
				break;
			}
			case Constants.DataSize.Dword:
			{
				adjustAmount = 4;
				break;
			}
			default:
			{
				result.AddError("ExecuteBLD", $"invalid size for BLD: {operation.DataSize}");
				break;
			}
		}

		// Increment/Decrement memory pointer (not port)
		if (operation.Increment)
		{
			hl += adjustAmount;
		}
		else
		{
			hl -= adjustAmount;
		}

		// Decrement counter and update flags
		bc--;

		ALUFlagState flagState = GetALUFlags();
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.ParityOverflow = bc != 0;
		UpdateALUFlags(flagState);

		// Writeback registers
		Registers.Write(Constants.RegisterTarget.BC, Constants.DataSize.Dword, bc);
		Registers.Write(Constants.RegisterTarget.DE, Constants.DataSize.Dword, de);
		Registers.Write(Constants.RegisterTarget.HL, Constants.DataSize.Dword, hl);

		// if Parity/Overflow is true (BC != 0), continue
		if (operation.Repeat && Registers.GetFlag(Constants.FlagMask.ParityOverflow))
		{
			// Undo PC increment
			uint pc = Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			pc -= operation.Length;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, pc);

			// Set up to repeat
			if (_currentBlockOperation is null)
			{
				// Further fetches cost nothing
				operation.FetchCycles = 0;
				_currentBlockOperation = operation;
			}
		}
		// Done
		else
		{
			_currentBlockOperation = null;
		}

		return result;
	}

	private Result<int> ExecuteBKPT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		Result<int> result = new(operation.FetchCycles + 1);

		uint code = operation.Destination.Data;
		_handleBreakpointInstruction(operation.BaseAddress, code);

		return result;
	}

	private Result<MemoryOperation> InternalPush(uint value)
	{
		uint sp = _registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword);
		sp -= 4;
		_registers.Write(Constants.RegisterTarget.SP, Constants.DataSize.Dword, sp);
		Result<MemoryOperation> writeMemoryResult = _memoryBus.Write(sp, Constants.DataSize.Dword, value);

		return writeMemoryResult;
	}

	private Result<MemoryOperation> InternalPop()
	{
		uint sp = _registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword);
		Result<MemoryOperation> readMemoryResult = _memoryBus.Read(sp, Constants.DataSize.Dword);
		sp += 4;
		_registers.Write(Constants.RegisterTarget.SP, Constants.DataSize.Dword, sp);

		return readMemoryResult;
	}

	private Result<int> InternalServiceInterrupt(byte interruptNumber)
	{
		Result<int> result = new(0);

		// Wake from halt if necessary
		_halted = false;

		// Cancel any executing block operation (will be re-fetched on return)
		_currentBlockOperation = null;

		// Push PC to the stack
		uint pc = Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		Result<MemoryOperation> pushResult = InternalPush(pc);
		result.Combine(pushResult);
		result.ResultObject += pushResult.ResultObject.Cycles;

		// Get the vector address from the IVT (I << 10) + (interruptNumber << 2)
		uint vectorAddress = Registers.Read(Constants.RegisterTarget.I, Constants.DataSize.Dword) << 10;
		vectorAddress |= (uint)(interruptNumber << 2);

		// Read the service routine vector from the vector address
		Result<MemoryOperation> vectorReadResult = _memoryBus.Read(vectorAddress, Constants.DataSize.Dword);
		result.Combine(vectorReadResult);
		result.ResultObject += vectorReadResult.ResultObject.Cycles;

		// Jump to the service routine vector
		Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, vectorReadResult.ResultObject.Data);

		return result;
	}
}
