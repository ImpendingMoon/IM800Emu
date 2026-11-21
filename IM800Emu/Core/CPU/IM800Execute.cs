using IM800Emu.Core.Bus;
using System.Diagnostics;

namespace IM800Emu.Core.CPU;

internal partial class IM800
{
	private int ExecuteInvalid(DecodedOperation operation)
	{
		int cycles = operation.FetchCycles + 1;

		_context.Events.RaiseInvalidInstruction(
			operation,
			"Invalid instruction"
		);
		if (_context.CancelOperation)
		{
			return cycles;
		}

		return cycles;
	}

	private int ExecuteHalted(DecodedOperation operation)
	{
		int cycles = 4;
		return cycles;
	}

	private int ExecuteInterrupted(DecodedOperation operation)
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

	private int ExecuteNonMaskableInterrupt(DecodedOperation operation)
	{
		_interruptBus.AcknowledgeNonMaskableInterrupt();

		bool enableInterrupts = Registers.GetFlag(Constants.FlagMask.EnableInterrupts);
		Registers.SetFlag(Constants.FlagMask.EnableInterruptsSave, enableInterrupts);
		Registers.SetFlag(Constants.FlagMask.EnableInterrupts, false);

		return InternalServiceInterrupt(2);
	}

	private int ExecuteLD(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		// Dest <- Source

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{
			return cycles;
		}

		cycles += readSourceResult.Cycles;

		MemoryResult writeDestResult = WriteOperand(
			operation.Destination,
			readSourceResult.Data
		);
		if (_context.CancelOperation)
		{
			return cycles;
		}

		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteEX(DecodedOperation operation)
	{
		// Dest <-> Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += readDestResult.Cycles;

		MemoryResult writeSourceResult = WriteOperand(
			operation.Source,
			readDestResult.Data
		);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += writeSourceResult.Cycles;

		MemoryResult writeDestResult = WriteOperand(
			operation.Destination,
			readSourceResult.Data
		);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecutePUSH(DecodedOperation operation)
	{
		// sp <- sp - 4
		// [sp] <- dest

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += readSourceResult.Cycles;

		MemoryResult pushResult = InternalPush(readSourceResult.Data);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += pushResult.Cycles;

		return cycles;
	}

	private int ExecutePOP(DecodedOperation operation)
	{
		// dest <- [sp]
		// sp <- sp + 4

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult popResult = InternalPop();
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += popResult.Cycles;

		MemoryResult writeDestinationResult = WriteOperand(
			operation.Destination,
			popResult.Data
		);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += writeDestinationResult.Cycles;

		return cycles;
	}

	private int ExecuteEXH(DecodedOperation operation)
	{
		// swap high and low halves of the operand

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readDestinationResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += readDestinationResult.Cycles;

		uint data = readDestinationResult.Data;
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
				cycles += Constants.DwordALUCost;
				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteESA(DecodedOperation operation)
	{
		// extend, shift, add
		// dest <- dest + extend(source << scale)

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += readDestResult.Cycles;

		byte shiftAmount = operation.DataSize switch
		{
			Constants.DataSize.Byte => 0,
			Constants.DataSize.Word => 1,
			Constants.DataSize.Dword => 2,
			Constants.DataSize.Qword => 3,
			_ => throw new InvalidOperationException($"impossible data size for instruction ESA: {operation.DataSize}")
		};

		uint source = readSourceResult.Data;
		source = BitHelper.SignExtend(source, 16);
		source <<= shiftAmount;

		uint data = readDestResult.Data + source;

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{
			return cycles;
		}
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteEXA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		if (operation.Destination.Register != default)
		{
			Registers.ExchangeWithAlternate(operation.Destination.Register, operation.Destination.DataSize);
		}
		else
		{
			_context.Events.RaiseInvalidInstruction(
				operation,
				$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
			);
			if (_context.CancelOperation)
			{
				return cycles;
			}

		}

		return cycles;
	}

	private int ExecuteEXX(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		Registers.ExchangeWithAlternate(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		Registers.ExchangeWithAlternate(Constants.RegisterTarget.DE, Constants.DataSize.Dword);
		Registers.ExchangeWithAlternate(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		return cycles;
	}

	private int ExecuteEXI(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		Registers.ExchangeWithAlternate(Constants.RegisterTarget.IX, Constants.DataSize.Dword);
		Registers.ExchangeWithAlternate(Constants.RegisterTarget.IY, Constants.DataSize.Dword);
		Registers.ExchangeWithAlternate(Constants.RegisterTarget.SP, Constants.DataSize.Dword);

		return cycles;
	}

	private int ExecuteIN_OUT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);
		Debug.Assert(operation.Destination.Indirect ^ operation.Source.Indirect);

		int cycles = operation.FetchCycles + 1;

		// OUT
		if (operation.Destination.Indirect)
		{
			MemoryResult readSourceResult = ReadOperand(operation.Source);
			if (_context.CancelOperation)
			{
				return cycles;
			}
			cycles += readSourceResult.Cycles;

			uint port = GetEffectiveAddress(operation.Destination);
			MemoryResult writePortResult = _ioBus.Write(
				port,
				operation.DataSize,
				readSourceResult.Data
			);
			if (_context.CancelOperation)
			{
				return cycles;
			}
			cycles += writePortResult.Cycles;
		}
		// IN
		else
		{
			uint port = GetEffectiveAddress(operation.Source);
			MemoryResult readPortResult = _ioBus.Read(port, operation.DataSize);
			if (_context.CancelOperation)
			{ return cycles; }
			cycles += readPortResult.Cycles;

			MemoryResult writeDestResult = WriteOperand(
				operation.Destination,
				readPortResult.Data
			);
			if (_context.CancelOperation)
			{ return cycles; }
			cycles += writeDestResult.Cycles;
		}

		return cycles;
	}

	private int ExecuteADD(DecodedOperation operation)
	{
		// Dest <- Dest + Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = (byte)readSourceResult.Data;

				data = (byte)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.Data;
				ushort b = (ushort)readSourceResult.Data;

				data = (ushort)(a + b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = readSourceResult.Data;

				data = a + b;

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteADC(DecodedOperation operation)
	{
		// Dest <- Dest + Source + Carry

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = (byte)readSourceResult.Data;
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
				ushort a = (ushort)readDestResult.Data;
				ushort b = (ushort)readSourceResult.Data;
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
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = readSourceResult.Data;
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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;
		return cycles;
	}

	private int ExecuteSUB(DecodedOperation operation)
	{
		// Dest <- Dest - Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = (byte)readSourceResult.Data;

				data = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.Data;
				ushort b = (ushort)readSourceResult.Data;

				data = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = readSourceResult.Data;

				data = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;
		return cycles;
	}

	private int ExecuteSBC(DecodedOperation operation)
	{
		// Dest <- Dest - Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = (byte)readSourceResult.Data;
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
				ushort a = (ushort)readDestResult.Data;
				ushort b = (ushort)readSourceResult.Data;

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
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = readSourceResult.Data;
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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;
		return cycles;
	}

	private int ExecuteCP(DecodedOperation operation)
	{
		// Dest - Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = (byte)readSourceResult.Data;

				data = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.Data;
				ushort b = (ushort)readSourceResult.Data;

				data = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = readSourceResult.Data;

				data = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		return cycles;
	}

	private int ExecuteINC(DecodedOperation operation)
	{
		// Dest <- Dest + 1

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = 1;

				data = (byte)(a + b);

				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.Data;
				ushort b = 1;

				data = (ushort)(a + b);

				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = 1;

				data = a + b;

				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteDEC(DecodedOperation operation)
	{
		// Dest <- Dest - 1

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = 1;

				data = (byte)(a - b);

				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.Data;
				ushort b = 1;

				data = (ushort)(a - b);

				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = 1;

				data = a - b;

				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;
		return cycles;
	}

	private int ExecuteNEG(DecodedOperation operation)
	{
		// Dest <- 0 - Dest

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = 0;
				byte b = (byte)readDestResult.Data;

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
				ushort b = (ushort)readDestResult.Data;

				data = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = 0;
				uint b = readDestResult.Data;

				data = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillSubtractionUnderflow(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = true;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;
		return cycles;
	}

	private int ExecuteEXT(DecodedOperation operation)
	{
		// Dest <- SignExtend(Dest, width / 2)

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;

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
				cycles += Constants.DwordALUCost;

				data = BitHelper.SignExtend(data, 16);
				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;
		return cycles;
	}

	private int ExecuteMLT(DecodedOperation operation)
	{
		// Dest <- Dest.Hi * Dest.Lo

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
			{
				int multiplicand = (int)(data & 0xFF);
				int multiplier = (int)((data >> 8) & 0xFF);

				int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
				cycles += cyclesPerIteration * 8; // 8 Iterations for 8*8 mult
				cycles += BitHelper.NumberOfOnes((uint)multiplicand); // Roughly number of adds/subs needed

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
				cycles += cyclesPerIteration * 16; // 16 Iterations for 16*16 mult
				cycles += BitHelper.NumberOfOnes((uint)multiplicand); // Roughly number of adds/subs needed

				data = (ushort)(multiplicand * multiplier);

				flagState.Carry = data > byte.MaxValue;
				flagState.Sign = (data & 0x8000) != 0;
				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;
		return cycles;
	}

	private int ExecuteDIV(DecodedOperation operation)
	{
		// Quotient = Dest.Hi / Dest.Lo
		// Remainder = Dest.Hi % Dest.Lo
		// Dest.Hi <- Remainter
		// Dest.Lo <- Quotient

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
			{
				uint dividend = (data >> 8) & 0xFF;
				uint divisor = data & 0xFF;

				// Decision
				cycles += 1;
				if (divisor == 0)
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, true);
				}
				else
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, false);

					int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
					cycles += cyclesPerIteration * 8; // 8 Iterations for 8/8 div
					cycles += BitHelper.NumberOfOnes(dividend); // Roughly number of adds/subs needed

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
				cycles += 1;
				if (divisor == 0)
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, true);
				}
				else
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, false);

					int cyclesPerIteration = 4 * Constants.DwordALUCost; // Branch, Shift, Loop Overhead
					cycles += cyclesPerIteration * 16; // 16 Iterations for 16/16 div
					cycles += BitHelper.NumberOfOnes(dividend); // Roughly number of adds/subs needed

					data = (ushort)(dividend / divisor); // Quotient in lower half
					data |= (uint)(ushort)(dividend % divisor) << 16; // Remainder in upper half
				}

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;
		return cycles;
	}

	private int ExecuteSDIV(DecodedOperation operation)
	{
		// Quotient = Dest.Hi / Dest.Lo
		// Remainder = Dest.Hi % Dest.Lo
		// Dest.Hi <- Remainter
		// Dest.Lo <- Quotient

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Word:
			{
				int dividend = (int)((data >> 8) & 0xFF);
				int divisor = (int)(data & 0xFF);

				// Decision
				cycles += 1;
				if (divisor == 0)
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, true);
				}
				else
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, false);

					int cyclesPerIteration = 4; // Branch, Shift, Loop Overhead
					cycles += cyclesPerIteration * 8; // 8 Iterations for 8/8 div
					cycles += BitHelper.NumberOfOnes((uint)dividend); // Roughly number of adds/subs needed

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
				cycles += 1;
				if (divisor == 0)
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, true);
				}
				else
				{
					Registers.SetFlag(Constants.FlagMask.ParityOverflow, false);

					int cyclesPerIteration = 4 * Constants.DwordALUCost; // Branch, Shift, Loop Overhead
					cycles += cyclesPerIteration * 16; // 16 Iterations for 16/16 div
					cycles += BitHelper.NumberOfOnes((uint)dividend); // Roughly number of adds/subs needed

					data = (ushort)(dividend / divisor); // Quotient in lower half
					data |= (uint)(ushort)(dividend % divisor) << 16; // Remainder in upper half
				}

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;
		return cycles;
	}

	// Implementation taken from "The Undocumented Z80 Documented" by Sean Young
	private int ExecuteDAA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		uint a = Registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Byte);

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
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);
		Registers.Write(Constants.RegisterTarget.A, Constants.DataSize.Byte, a);

		return cycles;
	}

	private int ExecuteAND(DecodedOperation operation)
	{
		// Dest <- Dest & Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = (byte)readSourceResult.Data;

				data = (byte)(a & b);

				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.Data;
				ushort b = (ushort)readSourceResult.Data;

				data = (ushort)(a & b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = readSourceResult.Data;

				data = a & b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteOR(DecodedOperation operation)
	{
		// Dest <- Dest | Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = (byte)readSourceResult.Data;

				data = (byte)(a | b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.Data;
				ushort b = (ushort)readSourceResult.Data;

				data = (ushort)(a | b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = readSourceResult.Data;

				data = a | b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteXOR(DecodedOperation operation)
	{
		// Dest <- Dest ^ Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = (byte)readSourceResult.Data;

				data = (byte)(a ^ b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.ParityOverflow = BitHelper.WillAdditionOverflow(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.Data;
				ushort b = (ushort)readSourceResult.Data;

				data = (ushort)(a ^ b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = readSourceResult.Data;

				data = a ^ b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteTST(DecodedOperation operation)
	{
		// Dest & Source

		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
				byte b = (byte)readSourceResult.Data;

				data = (byte)(a & b);

				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)readDestResult.Data;
				ushort b = (ushort)readSourceResult.Data;

				data = (ushort)(a & b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = readSourceResult.Data;

				data = a & b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		return cycles;
	}

	private int ExecuteCPL(DecodedOperation operation)
	{
		// Dest <- Dest ^ -1

		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		uint data = readDestResult.Data;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)readDestResult.Data;
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
				ushort a = (ushort)readDestResult.Data;
				ushort b = 0xFFFF;

				data = (ushort)(a ^ b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = readDestResult.Data;
				uint b = 0xFFFFFFFF;

				data = a ^ b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Carry = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.HalfCarry = true;
		flagState.Subtract = false;
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteBIT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Zero = ((readDestResult.Data >> bit) & 1) == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		return cycles;
	}

	private int ExecuteSET(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
		uint data = readDestResult.Data;

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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		data |= (uint)(1 << bit);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteRES(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
		uint data = readDestResult.Data;

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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		data &= ~(uint)(1 << bit);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteRLC(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
		uint data = readDestResult.Data;
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
					cycles++;
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
					cycles++;
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
					cycles += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteRRC(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
		uint data = readDestResult.Data;
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
					cycles++;
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
					cycles++;
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
					cycles += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteRL(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
		uint data = readDestResult.Data;
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
					cycles++;
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
					cycles++;
				}

				data = a;
				flagState.Sign = data != 0;

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
					cycles += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteRR(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
		uint data = readDestResult.Data;
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
					cycles++;
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
					cycles++;
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
					cycles += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteSLA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
		uint data = readDestResult.Data;
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
					cycles++;
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
					cycles++;
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
					cycles += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteSRA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
		uint data = readDestResult.Data;
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
					cycles++;
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
					cycles++;
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
					cycles += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteSRL(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is not null);

		int cycles = operation.FetchCycles + 1;

		MemoryResult readSourceResult = ReadOperand(operation.Source);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult readDestResult = ReadOperand(operation.Destination);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readDestResult.Cycles;

		byte bit = (byte)readSourceResult.Data;
		uint data = readDestResult.Data;
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
					cycles++;
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
					cycles++;
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
					cycles += Constants.DwordALUCost;
				}

				data = a;
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

				break;
			}
		}

		flagState.Subtract = false;
		flagState.ParityOverflow = BitHelper.IsParityEven(data);
		flagState.Zero = data == 0;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		MemoryResult writeDestResult = WriteOperand(operation.Destination, data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

		return cycles;
	}

	private int ExecuteRLD(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);
		MemoryResult readHLResult = _memoryBus.Read(hl, Constants.DataSize.Byte);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readHLResult.Cycles;

		byte a = (byte)Registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Byte);
		byte mem = (byte)readHLResult.Data;

		byte aLow = (byte)(a & 0x0F);
		byte memHigh = (byte)((mem >> 4) & 0x0F);
		byte memLow = (byte)(mem & 0x0F);

		byte newA = (byte)((a & 0xF0) | memHigh);
		byte newMem = (byte)((memLow << 4) | aLow);

		MemoryResult writeHLResult = _memoryBus.Write(hl, Constants.DataSize.Byte, newMem);
		cycles += writeHLResult.Cycles;

		ALUFlagState flagState = GetALUFlags();
		flagState.Sign = (newA & 0x80) != 0;
		flagState.Zero = newA == 0;
		flagState.ParityOverflow = BitHelper.IsParityEven(newA);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		Registers.Write(Constants.RegisterTarget.A, Constants.DataSize.Byte, newA);

		cycles += 2;
		return cycles;
	}

	private int ExecuteRRD(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);
		MemoryResult readHLResult = _memoryBus.Read(hl, Constants.DataSize.Byte);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readHLResult.Cycles;

		byte a = (byte)Registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Byte);
		byte mem = (byte)readHLResult.Data;

		byte aLow = (byte)(a & 0x0F);
		byte memHigh = (byte)((mem >> 4) & 0x0F);
		byte memLow = (byte)(mem & 0x0F);

		byte newA = (byte)((a & 0xF0) | memLow);
		byte newMem = (byte)((aLow << 4) | memHigh);

		MemoryResult writeHLResult = _memoryBus.Write(hl, Constants.DataSize.Byte, newMem);
		cycles += writeHLResult.Cycles;

		ALUFlagState flagState = GetALUFlags();
		flagState.Sign = (newA & 0x80) != 0;
		flagState.Zero = newA == 0;
		flagState.ParityOverflow = BitHelper.IsParityEven(newA);
		flagState.HalfCarry = false;
		flagState.Subtract = false;
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
		UpdateALUFlags(flagState);

		Registers.Write(Constants.RegisterTarget.A, Constants.DataSize.Byte, newA);

		cycles += 2;
		return cycles;
	}

	private int ExecuteNOP(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);
		int cycles = operation.FetchCycles + 1;
		return cycles;
	}

	private int ExecuteJP(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		if (IsConditionTrue(operation.Condition))
		{
			MemoryResult addressReadResult = ReadOperand(operation.Destination);
			if (_context.CancelOperation)
			{ return cycles; }
			cycles += addressReadResult.Cycles;

			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, addressReadResult.Data);
		}

		return cycles;
	}

	private int ExecuteJR(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		if (IsConditionTrue(operation.Condition))
		{
			MemoryResult addressReadResult = ReadOperand(operation.Destination);
			if (_context.CancelOperation)
			{ return cycles; }
			cycles += addressReadResult.Cycles;

			int displacement = 0;

			if (operation.DataSize == Constants.DataSize.Byte)
			{
				displacement = (int)BitHelper.SignExtend(addressReadResult.Data, 8);
			}
			else if (operation.DataSize == Constants.DataSize.Word)
			{
				displacement = (int)BitHelper.SignExtend(addressReadResult.Data, 16);
			}
			else
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

			}

			int pc = (int)Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			pc += displacement;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, (uint)pc);
		}

		return cycles;
	}

	private int ExecuteDJNZ(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		ushort b = (ushort)Registers.Read(Constants.RegisterTarget.B, Constants.DataSize.Word);
		b--;
		Registers.Write(Constants.RegisterTarget.B, Constants.DataSize.Word, b);

		if (b != 0)
		{
			int pc = (int)Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			int displacement = (int)BitHelper.SignExtend(operation.Destination.Data, 8);
			pc += displacement;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, (uint)pc);
			cycles += 2;
		}

		return cycles;
	}

	private int ExecuteJAZ(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		ushort a = (ushort)Registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Word);

		if (a == 0)
		{
			int pc = (int)Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			int displacement = (int)BitHelper.SignExtend(operation.Destination.Data, 8);
			pc += displacement;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, (uint)pc);
			cycles += 2;
		}

		return cycles;
	}

	private int ExecuteJANZ(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		ushort a = (ushort)Registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Word);

		if (a != 0)
		{
			int pc = (int)Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			int displacement = (int)BitHelper.SignExtend(operation.Destination.Data, 8);
			pc += displacement;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, (uint)pc);
			cycles += 2;
		}

		return cycles;
	}

	private int ExecuteCALL(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		if (IsConditionTrue(operation.Condition))
		{
			uint pc = Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			MemoryResult pushResult = InternalPush(pc);
			if (_context.CancelOperation)
			{ return cycles; }
			cycles += pushResult.Cycles;

			MemoryResult addressReadResult = ReadOperand(operation.Destination);
			if (_context.CancelOperation)
			{ return cycles; }
			cycles += addressReadResult.Cycles;

			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, addressReadResult.Data);
		}

		return cycles;
	}

	private int ExecuteCR(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		if (IsConditionTrue(operation.Condition))
		{
			uint pc = Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
			MemoryResult pushResult = InternalPush(pc);
			if (_context.CancelOperation)
			{ return cycles; }
			cycles += pushResult.Cycles;

			MemoryResult addressReadResult = ReadOperand(operation.Destination);
			if (_context.CancelOperation)
			{ return cycles; }
			cycles += addressReadResult.Cycles;

			int displacement = 0;

			if (operation.DataSize == Constants.DataSize.Byte)
			{
				displacement = (int)BitHelper.SignExtend(addressReadResult.Data, 8);
			}
			else if (operation.DataSize == Constants.DataSize.Word)
			{
				displacement = (int)BitHelper.SignExtend(addressReadResult.Data, 16);
			}
			else
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

			}

			pc = (uint)((int)pc + displacement);
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, pc);
		}

		return cycles;
	}

	private int ExecuteRET(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		if (IsConditionTrue(operation.Condition))
		{
			MemoryResult popResult = InternalPop();
			if (_context.CancelOperation)
			{ return cycles; }
			cycles += popResult.Cycles;
			Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, popResult.Data);
		}

		return cycles;
	}

	private int ExecuteRETI(DecodedOperation operation)
	{
		int cycles = operation.FetchCycles + 1;

		_interruptBus.CompleteInterrupt();

		MemoryResult popResult = InternalPop();
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += popResult.Cycles;
		Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, popResult.Data);

		return cycles;
	}

	private int ExecuteRETN(DecodedOperation operation)
	{
		int cycles = operation.FetchCycles + 1;

		bool enableInterrupts = Registers.GetFlag(Constants.FlagMask.EnableInterruptsSave);
		Registers.SetFlag(Constants.FlagMask.EnableInterrupts, enableInterrupts);

		MemoryResult popResult = InternalPop();
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += popResult.Cycles;
		Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, popResult.Data);

		return cycles;
	}

	private int ExecuteRST(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		int serviceInterruptCycles = InternalServiceInterrupt((byte)operation.Destination.Data);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += serviceInterruptCycles;

		return cycles;
	}

	private int ExecuteSCF(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		Registers.SetFlag(Constants.FlagMask.Carry, true);

		return cycles;
	}

	private int ExecuteCCF(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		bool carry = Registers.GetFlag(Constants.FlagMask.Carry);
		Registers.SetFlag(Constants.FlagMask.Carry, !carry);

		return cycles;
	}

	private int ExecuteEI(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		_pendingEnableInterrupts = true;

		return cycles;
	}

	private int ExecuteDI(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		Registers.SetFlag(Constants.FlagMask.EnableInterrupts, false);

		return cycles;
	}

	private int ExecuteIM1(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		_interruptMode = 1;

		return cycles;
	}

	private int ExecuteIM2(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		_interruptMode = 2;

		return cycles;
	}

	private int ExecuteHALT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		_halted = true;

		return cycles;
	}

	private int ExecuteLDI(DecodedOperation operation)
	{
		// decode only grabs the immediate in destination
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		// I is only 22 bits long (shifted left 10 to form upper bits of IVT address)
		uint newI = operation.Destination.Data & 0b1111111111111111111111;
		Registers.Write(Constants.RegisterTarget.I, Constants.DataSize.Dword, newI);

		return cycles;
	}

	private int ExecuteLDAR(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		uint r = Registers.Read(Constants.RegisterTarget.R, Constants.DataSize.Word);
		Registers.Write(Constants.RegisterTarget.A, Constants.DataSize.Word, r);

		return cycles;
	}

	private int ExecuteLDRA(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		uint a = Registers.Read(Constants.RegisterTarget.A, Constants.DataSize.Word);
		Registers.Write(Constants.RegisterTarget.R, Constants.DataSize.Word, a);

		return cycles;
	}

	private int ExecuteBLD(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		uint bc = Registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		uint de = Registers.Read(Constants.RegisterTarget.DE, Constants.DataSize.Dword);
		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		MemoryResult readSourceResult = _memoryBus.Read(hl, operation.DataSize);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		MemoryResult writeDestResult = _memoryBus.Write(
			de,
			operation.DataSize,
			readSourceResult.Data
		);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

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
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
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

		return cycles;
	}

	private int ExecuteBCP(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

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

		MemoryResult readSourceResult = _memoryBus.Read(hl, operation.DataSize);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		uint data = af;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)af;
				byte b = (byte)readSourceResult.Data;

				data = (byte)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)af;
				ushort b = (ushort)readSourceResult.Data;

				data = (ushort)(a - b);

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = af;
				uint b = readSourceResult.Data;

				data = a - b;

				flagState.Carry = BitHelper.WillSubtractionWrap(a, b);
				flagState.HalfCarry = BitHelper.WillSubtractionHalfCarry(a, b);
				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

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
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
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

		return cycles;
	}

	private int ExecuteBTST(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

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

		MemoryResult readSourceResult = _memoryBus.Read(hl, operation.DataSize);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readSourceResult.Cycles;

		uint data = af;
		ALUFlagState flagState = GetALUFlags();

		switch (operation.DataSize)
		{
			case Constants.DataSize.Byte:
			{
				byte a = (byte)af;
				byte b = (byte)readSourceResult.Data;

				data = (byte)(a & b);

				flagState.Carry = BitHelper.WillAdditionWrap(a, b);
				flagState.HalfCarry = BitHelper.WillAdditionHalfCarry(a, b);
				flagState.Sign = (data & 0x80) != 0;

				break;
			}
			case Constants.DataSize.Word:
			{
				ushort a = (ushort)af;
				ushort b = (ushort)readSourceResult.Data;

				data = (ushort)(a & b);

				flagState.Sign = (data & 0x8000) != 0;

				break;
			}
			case Constants.DataSize.Dword:
			{
				cycles += Constants.DwordALUCost;

				uint a = af;
				uint b = readSourceResult.Data;

				data = a & b;

				flagState.Sign = (data & 0x80000000) != 0;

				break;
			}
			default:
			{
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

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
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
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

		return cycles;
	}

	private int ExecuteBIN(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		uint bc = Registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		uint de = Registers.Read(Constants.RegisterTarget.DE, Constants.DataSize.Dword);
		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		MemoryResult readPortResult = _ioBus.Read(hl, operation.DataSize);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readPortResult.Cycles;

		MemoryResult writeDestResult = _memoryBus.Write(
			de,
			operation.DataSize,
			readPortResult.Data
		);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writeDestResult.Cycles;

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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

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
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
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

		return cycles;
	}

	private int ExecuteBOUT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		uint bc = Registers.Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword);
		uint de = Registers.Read(Constants.RegisterTarget.DE, Constants.DataSize.Dword);
		uint hl = Registers.Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword);

		MemoryResult readPortResult = _ioBus.Read(hl, operation.DataSize);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += readPortResult.Cycles;

		MemoryResult writePortResult = _ioBus.Write(
			de,
			operation.DataSize,
			readPortResult.Data
		);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += writePortResult.Cycles;

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
				_context.Events.RaiseInvalidInstruction(
					operation,
					$"invalid size for instruction {operation.Operation}: {operation.DataSize}"
				);
				if (_context.CancelOperation)
				{
					return cycles;
				}

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
		flagState.Less = flagState.Sign ^ flagState.ParityOverflow;
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

		return cycles;
	}

	private int ExecuteBKPT(DecodedOperation operation)
	{
		Debug.Assert(operation.Destination is not null && operation.Source is null);

		int cycles = operation.FetchCycles + 1;

		_context.Events.RaiseBkptInstruction(operation);
		if (_context.CancelOperation)
		{
			return cycles;
		}

		return cycles;
	}

	private MemoryResult InternalPush(uint value)
	{
		uint sp = Registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword);
		sp -= 4;
		Registers.Write(Constants.RegisterTarget.SP, Constants.DataSize.Dword, sp);
		MemoryResult writeMemoryResult = _memoryBus.Write(sp, Constants.DataSize.Dword, value);

		return writeMemoryResult;
	}

	private MemoryResult InternalPop()
	{
		uint sp = Registers.Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword);
		MemoryResult readMemoryResult = _memoryBus.Read(sp, Constants.DataSize.Dword);
		sp += 4;
		Registers.Write(Constants.RegisterTarget.SP, Constants.DataSize.Dword, sp);

		return readMemoryResult;
	}

	private int InternalServiceInterrupt(byte interruptNumber)
	{
		int cycles = 0;

		// Wake from halt if necessary
		_halted = false;

		// Cancel any executing block operation (will be re-fetched on return)
		_currentBlockOperation = null;

		// Push PC to the stack
		uint pc = Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		MemoryResult pushResult = InternalPush(pc);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += pushResult.Cycles;

		// Get the vector address from the IVT (I << 10) + (interruptNumber << 2)
		uint vectorAddress = Registers.Read(Constants.RegisterTarget.I, Constants.DataSize.Dword) << 10;
		vectorAddress |= (uint)(interruptNumber << 2);

		// Read the service routine vector from the vector address
		MemoryResult vectorReadResult = _memoryBus.Read(vectorAddress, Constants.DataSize.Dword);
		if (_context.CancelOperation)
		{ return cycles; }
		cycles += vectorReadResult.Cycles;

		// Jump to the service routine vector
		Registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, vectorReadResult.Data);

		return cycles;
	}
}
