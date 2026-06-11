using IM800Emu.Core.Bus;

namespace IM800Emu.Core.CPU;

public partial class IM800
{
	private void DecodeFormatR(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 7:2 Opcode
		// 9:8 Size
		// 12:10 Dest register
		// 15:13 Src register

		decodeResult.ResultObject.Destination = new();
		decodeResult.ResultObject.Source = new();

		int opcode = (decodeResult.ResultObject.InstructionWord >> 2) & 0b111111;
		decodeResult.ResultObject.Operation = opcode switch
		{
			0b000000 => Constants.Operation.LD,
			0b000001 => Constants.Operation.EX,
			// ...
			0b000100 => Constants.Operation.ADD,
			0b000101 => Constants.Operation.ADC,
			0b000110 => Constants.Operation.SUB,
			0b000111 => Constants.Operation.SBC,
			0b001000 => Constants.Operation.AND,
			0b001001 => Constants.Operation.OR,
			0b001010 => Constants.Operation.XOR,
			0b001011 => Constants.Operation.CP,
			0b001100 => Constants.Operation.TST,
			// ...
			0b010000 => Constants.Operation.LEA,
			0b010001 => Constants.Operation.BIT,
			0b010010 => Constants.Operation.SET,
			0b010011 => Constants.Operation.RES,
			0b010100 => Constants.Operation.RLC,
			0b010101 => Constants.Operation.RRC,
			0b010110 => Constants.Operation.RL,
			0b010111 => Constants.Operation.RR,
			0b011000 => Constants.Operation.SLA,
			0b011001 => Constants.Operation.SRA,
			0b011010 => Constants.Operation.SRL,
			_ => Constants.Operation.Invalid,
		};

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			decodeResult.AddError(_decodeErrorName, $"invalid format R opcode 0x{opcode:X2}");
			return;
		}

		int sizeSelector = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodeResult.ResultObject.DataSize = size;

		// Only LEA can use a Qword operand size
		if (size == Constants.DataSize.Qword && decodeResult.ResultObject.Operation != Constants.Operation.LEA)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"invalid size {size} for operation {decodeResult.ResultObject.Operation}"
			);
			return;
		}

		// Instructions in this range use the size field for both operands
		if (opcode < 0b010000)
		{
			decodeResult.ResultObject.Destination.DataSize = size;
			decodeResult.ResultObject.Source.DataSize = size;
		}
		// LEA uses fixed sizes and reinterprets instruction size
		else if (opcode == 0b010000)
		{
			decodeResult.ResultObject.Destination.DataSize = Constants.DataSize.Dword;
			decodeResult.ResultObject.Source.DataSize = Constants.DataSize.Word;
		}
		// Bit/shift instructions use byte-sized sources (source is shift amount)
		else
		{
			decodeResult.ResultObject.Destination.DataSize = size;
			decodeResult.ResultObject.Source.DataSize = Constants.DataSize.Byte;
		}

		int destinationSelector = (decodeResult.ResultObject.InstructionWord >> 10) & 0b111;

		if (destinationSelector == (int)Constants.RegisterSelector.Immediate)
		{
			decodeResult.AddError(_decodeErrorName, $"destination register cannot be immediate");
			return;
		}

		decodeResult.ResultObject.Destination.Register = DecodeRegister(
			destinationSelector,
			decodeResult.ResultObject.Destination.DataSize
		);

		int sourceSelector = (decodeResult.ResultObject.InstructionWord >> 13) & 0b111;

		if (sourceSelector == (int)Constants.RegisterSelector.Immediate)
		{
			Result<MemoryOperation> immediateResult = FetchImmediate(
				decodeResult,
				decodeResult.ResultObject.Source.DataSize
			);

			if (!immediateResult.IsSuccess)
			{
				decodeResult.Combine(immediateResult);
				return;
			}
		}
		else
		{
			decodeResult.ResultObject.Source.Register = DecodeRegister(
				sourceSelector,
				decodeResult.ResultObject.Source.DataSize
			);
		}
	}

	private void DecodeFormatRM(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 6:2 Opcode
		// 7 Direction
		// 9:8 Size
		// 12:10 Register
		// 15:13 Address register

		decodeResult.ResultObject.Destination = new();
		decodeResult.ResultObject.Source = new();

		int opcode = (decodeResult.ResultObject.InstructionWord >> 2) & 0b11111;
		decodeResult.ResultObject.Operation = opcode switch
		{
			0b00000 => Constants.Operation.LD,
			0b00001 => Constants.Operation.EX,
			0b00010 => Constants.Operation.IN_OUT,
			// ...
			0b00100 => Constants.Operation.ADD,
			0b00101 => Constants.Operation.ADC,
			0b00110 => Constants.Operation.SUB,
			0b00111 => Constants.Operation.SBC,
			0b01000 => Constants.Operation.AND,
			0b01001 => Constants.Operation.OR,
			0b01010 => Constants.Operation.XOR,
			0b01011 => Constants.Operation.CP,
			0b01100 => Constants.Operation.TST,
			// ...
			0b10000 => Constants.Operation.LEA,
			0b10001 => Constants.Operation.BIT,
			0b10010 => Constants.Operation.SET,
			0b10011 => Constants.Operation.RES,
			0b10100 => Constants.Operation.RLC,
			0b10101 => Constants.Operation.RRC,
			0b10110 => Constants.Operation.RL,
			0b10111 => Constants.Operation.RR,
			0b11000 => Constants.Operation.SLA,
			0b11001 => Constants.Operation.SRA,
			0b11010 => Constants.Operation.SRL,
			_ => Constants.Operation.Invalid,
		};

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			decodeResult.AddError(_decodeErrorName, $"invalid format RM opcode 0x{opcode:X2}");
			return;
		}

		bool store = ((decodeResult.ResultObject.InstructionWord >> 7) & 1) == 1;

		int sizeSelector = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodeResult.ResultObject.DataSize = size;

		// Only LEA can use a Qword operand size
		if (size == Constants.DataSize.Qword && decodeResult.ResultObject.Operation != Constants.Operation.LEA)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"invalid size {size} for operation {decodeResult.ResultObject.Operation}"
			);
			return;
		}

		// Instructions in this range use the size field for both operands
		if (opcode < 0b10000)
		{
			decodeResult.ResultObject.Destination.DataSize = size;
			decodeResult.ResultObject.Source.DataSize = size;
		}
		// LEA uses fixed sizes and reinterprets instruction size
		else if (opcode == 0b10000)
		{
			decodeResult.ResultObject.Destination.DataSize = Constants.DataSize.Dword;
			decodeResult.ResultObject.Source.DataSize = Constants.DataSize.Word;
		}
		// Bit/shift instructions use byte-sized sources (source is shift amount)
		else
		{
			decodeResult.ResultObject.Destination.DataSize = size;
			decodeResult.ResultObject.Source.DataSize = Constants.DataSize.Byte;
		}

		int registerSelector = (decodeResult.ResultObject.InstructionWord >> 10) & 0b111;
		int addressRegisterSelector = (decodeResult.ResultObject.InstructionWord >> 12) & 0b111;

		if (
			registerSelector == (int)Constants.RegisterSelector.Immediate
			&& addressRegisterSelector == (int)Constants.RegisterSelector.Immediate
		)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"cannot use an immediate value for both register and address register"
			);
			return;
		}

		if (store)
		{
			// Address register is the decodeResult.ResultObject.Destination
			decodeResult.ResultObject.Destination.Indirect = true;

			if (addressRegisterSelector == (int)Constants.RegisterSelector.Immediate)
			{
				Result<MemoryOperation> immediateResult = FetchImmediate(decodeResult, Constants.DataSize.Dword);

				if (!immediateResult.IsSuccess)
				{
					decodeResult.Combine(immediateResult);
					return;
				}

				decodeResult.ResultObject.Destination.Data = immediateResult.ResultObject.Data;
			}
			else
			{
				decodeResult.ResultObject.Destination.Register = DecodeRegister(
					addressRegisterSelector,
					Constants.DataSize.Dword
				);

				if (
					addressRegisterSelector
					is (int)Constants.RegisterSelector.IX
					or (int)Constants.RegisterSelector.IY
					or (int)Constants.RegisterSelector.SP
				)
				{
					Result<MemoryOperation> displacementResult = FetchImmediate(decodeResult, Constants.DataSize.Word);

					if (!displacementResult.IsSuccess)
					{
						decodeResult.Combine(displacementResult);
						return;
					}

					decodeResult.ResultObject.Destination.Displacement = (short)displacementResult.ResultObject.Data;
				}
			}

			// Register is the source
			if (registerSelector == (int)Constants.RegisterSelector.Immediate)
			{
				Result<MemoryOperation> immediateResult = FetchImmediate(
					decodeResult,
					decodeResult.ResultObject.Source.DataSize
				);

				if (!immediateResult.IsSuccess)
				{
					decodeResult.Combine(immediateResult);
					return;
				}

				decodeResult.ResultObject.Source.Data = immediateResult.ResultObject.Data;
			}
			else
			{
				decodeResult.ResultObject.Source.Register = DecodeRegister(
					registerSelector,
					decodeResult.ResultObject.Source.DataSize
				);
			}
		}
		else
		{
			// Address register is the source
			decodeResult.ResultObject.Source.Indirect = true;

			if (addressRegisterSelector == (int)Constants.RegisterSelector.Immediate)
			{
				Result<MemoryOperation> immediateResult = FetchImmediate(decodeResult, Constants.DataSize.Dword);

				if (!immediateResult.IsSuccess)
				{
					decodeResult.Combine(immediateResult);
					return;
				}

				decodeResult.ResultObject.Source.Data = immediateResult.ResultObject.Data;
			}
			else
			{
				decodeResult.ResultObject.Source.Register = DecodeRegister(
					addressRegisterSelector,
					Constants.DataSize.Dword
				);

				if (
					addressRegisterSelector
					is (int)Constants.RegisterSelector.IX
					or (int)Constants.RegisterSelector.IY
					or (int)Constants.RegisterSelector.SP
				)
				{
					Result<MemoryOperation> displacementResult = FetchImmediate(decodeResult, Constants.DataSize.Word);

					if (!displacementResult.IsSuccess)
					{
						decodeResult.Combine(displacementResult);
						return;
					}

					decodeResult.ResultObject.Source.Displacement = (short)displacementResult.ResultObject.Data;
				}
			}

			// Register is the decodeResult.ResultObject.Destination
			if (registerSelector == (int)Constants.RegisterSelector.Immediate)
			{
				Result<MemoryOperation> immediateResult = FetchImmediate(
					decodeResult,
					decodeResult.ResultObject.Destination.DataSize
				);

				if (!immediateResult.IsSuccess)
				{
					decodeResult.Combine(immediateResult);
					return;
				}

				decodeResult.ResultObject.Destination.Data = immediateResult.ResultObject.Data;
			}
			else
			{
				decodeResult.ResultObject.Destination.Register = DecodeRegister(
					registerSelector,
					decodeResult.ResultObject.Source.DataSize
				);
			}
		}
	}

	private void DecodeFormatUR(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
		// 9:8 Size
		// 12:10 Register
		// 15:13 Function

		decodeResult.ResultObject.Destination = new()
		{
			Indirect = true
		};
		int opcode = (decodeResult.ResultObject.InstructionWord >> 4) & 0b1111;
		int function = (decodeResult.ResultObject.InstructionWord >> 13) & 0b111;
		int operationSelector = (opcode << 3) | function;
		decodeResult.ResultObject.Operation = operationSelector switch
		{
			0b0000_000 => Constants.Operation.EXA,
			0b0000_001 => Constants.Operation.PUSH,
			0b0000_010 => Constants.Operation.POP,
			0b0000_100 => Constants.Operation.EXH,
			0b0000_101 => Constants.Operation.EXT,
			0b0000_110 => Constants.Operation.INC,
			0b0000_111 => Constants.Operation.DEC,
			0b0001_000 => Constants.Operation.CPL,
			0b0001_001 => Constants.Operation.NEG,
			0b0001_010 => Constants.Operation.MLT,
			0b0001_011 => Constants.Operation.DIV,
			0b0001_100 => Constants.Operation.SDIV,
			_ => Constants.Operation.Invalid,
		};

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"invalid format UR opcode and function 0x{opcode:X2}, 0x{function:X2}"
			);
			return;
		}

		int sizeSelector = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodeResult.ResultObject.DataSize = size;
		decodeResult.ResultObject.Destination.DataSize = size;

		if (size == Constants.DataSize.Qword)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"invalid size {size} for operation {decodeResult.ResultObject.Operation}"
			);
			return;
		}

		int registerSelector = (decodeResult.ResultObject.InstructionWord >> 10) & 0b111;

		if (registerSelector == (int)Constants.RegisterSelector.Immediate)
		{
			decodeResult.AddError(_decodeErrorName, $"Format UR cannot use an immediate value");
			return;
		}

		decodeResult.ResultObject.Destination.Register = DecodeRegister(
			registerSelector,
			decodeResult.ResultObject.Destination.DataSize
		);
	}

	private void DecodeFormatUM(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
		// 9:8 Size
		// 12:10 Function
		// 15:13 Address register

		decodeResult.ResultObject.Destination = new()
		{
			Indirect = true
		};

		int opcode = (decodeResult.ResultObject.InstructionWord >> 4) & 0b1111;
		int function = (decodeResult.ResultObject.InstructionWord >> 10) & 0b111;
		int operationSelector = (opcode << 3) | function;
		decodeResult.ResultObject.Operation = operationSelector switch
		{
			0b0000_100 => Constants.Operation.EXH,
			0b0000_101 => Constants.Operation.EXT,
			0b0000_110 => Constants.Operation.INC,
			0b0000_111 => Constants.Operation.DEC,
			0b0001_000 => Constants.Operation.CPL,
			0b0001_001 => Constants.Operation.NEG,
			0b0001_010 => Constants.Operation.MLT,
			0b0001_011 => Constants.Operation.DIV,
			0b0001_100 => Constants.Operation.SDIV,
			_ => Constants.Operation.Invalid,
		};

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"invalid format UR opcode and function 0x{opcode:X2}, 0x{function:X2}"
			);
			return;
		}

		int sizeSelector = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodeResult.ResultObject.DataSize = size;
		decodeResult.ResultObject.Destination.DataSize = size;

		if (size == Constants.DataSize.Qword)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"invalid size {size} for operation {decodeResult.ResultObject.Operation}"
			);
			return;
		}

		int addressRegisterSelector = (decodeResult.ResultObject.InstructionWord >> 13) & 0b111;

		if (addressRegisterSelector == (int)Constants.RegisterSelector.Immediate)
		{
			Result<MemoryOperation> immediateResult = FetchImmediate(decodeResult, Constants.DataSize.Dword);

			if (!immediateResult.IsSuccess)
			{
				decodeResult.Combine(immediateResult);
				return;
			}

			decodeResult.ResultObject.Destination.Data = immediateResult.ResultObject.Data;
		}
		else
		{
			decodeResult.ResultObject.Destination.Register = DecodeRegister(
				addressRegisterSelector,
				Constants.DataSize.Dword
			);

			if (
				addressRegisterSelector
				is (int)Constants.RegisterSelector.IX
				or (int)Constants.RegisterSelector.IY
				or (int)Constants.RegisterSelector.SP
			)
			{
				Result<MemoryOperation> displacementResult = FetchImmediate(decodeResult, Constants.DataSize.Word);

				if (!displacementResult.IsSuccess)
				{
					decodeResult.Combine(displacementResult);
					return;
				}

				decodeResult.ResultObject.Destination.Displacement = (short)displacementResult.ResultObject.Data;
			}
		}
	}

	private void DecodeFormatB(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 8:4 Opcode
		// 12:9 Condition
		// 15:13 Address register

		decodeResult.ResultObject.Destination = new();

		int opcode = (decodeResult.ResultObject.InstructionWord >> 4) & 0b11111;

		switch (opcode)
		{
			case 0b00000:
			{
				decodeResult.ResultObject.Operation = Constants.Operation.JR;
				decodeResult.ResultObject.DataSize = Constants.DataSize.Byte;
				break;
			}
			case 0b00001:
			{
				decodeResult.ResultObject.Operation = Constants.Operation.JR;
				decodeResult.ResultObject.DataSize = Constants.DataSize.Word;
				break;
			}
			case 0b00010:
			{
				decodeResult.ResultObject.Operation = Constants.Operation.JP;
				decodeResult.ResultObject.DataSize = Constants.DataSize.Dword;
				break;
			}
			case 0b00100:
			{
				decodeResult.ResultObject.Operation = Constants.Operation.CR;
				decodeResult.ResultObject.DataSize = Constants.DataSize.Byte;
				break;
			}
			case 0b00101:
			{
				decodeResult.ResultObject.Operation = Constants.Operation.CR;
				decodeResult.ResultObject.DataSize = Constants.DataSize.Word;
				break;
			}
			case 0b00110:
			{
				decodeResult.ResultObject.Operation = Constants.Operation.CALL;
				decodeResult.ResultObject.DataSize = Constants.DataSize.Dword;
				break;
			}
			case 0b01000:
			{
				decodeResult.ResultObject.Operation = Constants.Operation.RET;
				break;
			}
			default:
			{
				decodeResult.ResultObject.Operation = Constants.Operation.Invalid;
				decodeResult.ResultObject.DataSize = Constants.DataSize.Dword;
				break;
			}
		}

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			decodeResult.AddError(_decodeErrorName, $"invalid format B opcode 0x{opcode:X2}");
			return;
		}

		int conditionSelector = (decodeResult.ResultObject.InstructionWord >> 9) & 0b1111;
		Result<Constants.Condition?> conditionResult = DecodeCondition(conditionSelector);

		if (!conditionResult.IsSuccess)
		{
			decodeResult.Combine(conditionResult);
			return;
		}

		if (conditionResult.ResultObject is not null)
		{
			decodeResult.ResultObject.Condition = conditionResult.ResultObject.Value;
		}

		int registerSelector = (decodeResult.ResultObject.InstructionWord >> 13) & 0b111;

		if (decodeResult.ResultObject.Operation == Constants.Operation.RET && registerSelector != 0)
		{
			decodeResult.AddError(_decodeErrorName, $"RET must have a register selector of 0");
			return;
		}

		if (registerSelector == (int)Constants.RegisterSelector.Immediate)
		{
			Result<MemoryOperation> immediateResult = FetchImmediate(decodeResult, decodeResult.ResultObject.DataSize);

			if (!immediateResult.IsSuccess)
			{
				decodeResult.Combine(immediateResult);
				return;
			}

			decodeResult.ResultObject.Destination.Data = immediateResult.ResultObject.Data;
		}
		else
		{
			decodeResult.ResultObject.Destination.Register = DecodeRegister(
				registerSelector,
				decodeResult.ResultObject.Destination.DataSize
			);
		}
	}

	private void DecodeFormatM(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
		// 15:8 Function

		int opcode = (decodeResult.ResultObject.InstructionWord >> 4) & 0b1111;
		int function = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11111111;
		int operationSelector = (opcode << 8) | function;

		decodeResult.ResultObject.Operation = operationSelector switch
		{
			0b0000_00000000 => Constants.Operation.SCF,
			0b0000_00000001 => Constants.Operation.CCF,
			0b0000_00000010 => Constants.Operation.DAA,
			0b0000_00000011 => Constants.Operation.RLD,
			0b0000_00000100 => Constants.Operation.RRD,
			0b0000_00000101 => Constants.Operation.RST,
			0b0001_00000000 => Constants.Operation.EXX,
			0b0001_00000001 => Constants.Operation.EXI,
			0b0001_00000010 => Constants.Operation.EI,
			0b0001_00000011 => Constants.Operation.DI,
			0b0001_00000100 => Constants.Operation.IM1,
			0b0001_00000101 => Constants.Operation.IM2,
			0b0001_00000110 => Constants.Operation.LDI,
			0b0001_00000111 => Constants.Operation.RETI,
			0b0001_00001000 => Constants.Operation.RETN,
			0b0001_00001001 => Constants.Operation.HALT,
			0b0001_00001010 => Constants.Operation.LDAR,
			0b0001_00001011 => Constants.Operation.LDRA,
			_ => Constants.Operation.Invalid,
		};

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"invalid format M opcode and function 0x{opcode:X2}, 0x{function:X2}"
			);
			return;
		}

		// Some instructions have additional data
		if (decodeResult.ResultObject.Operation == Constants.Operation.LDI)
		{
			Result<MemoryOperation> immediateResult = FetchImmediate(decodeResult, Constants.DataSize.Dword);

			if (!immediateResult.IsSuccess)
			{
				decodeResult.Combine(immediateResult);
				return;
			}

			decodeResult.ResultObject.Destination = new()
			{
				DataSize = Constants.DataSize.Dword,
				Data = immediateResult.ResultObject.Data,
			};
		}
		else if (decodeResult.ResultObject.Operation == Constants.Operation.RST)
		{
			Result<MemoryOperation> immediateResult = FetchImmediate(decodeResult, Constants.DataSize.Byte);

			if (!immediateResult.IsSuccess)
			{
				decodeResult.Combine(immediateResult);
				return;
			}

			decodeResult.ResultObject.Destination = new()
			{
				DataSize = Constants.DataSize.Byte,
				Data = immediateResult.ResultObject.Data,
			};
		}
	}

	private void DecodeFormatSB(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode

		int opcode = (decodeResult.ResultObject.InstructionWord >> 4) & 0b1111;

		decodeResult.ResultObject.Operation = opcode switch
		{
			0b0000 => Constants.Operation.NOP,
			0b0001 => Constants.Operation.DJNZ,
			0b0010 => Constants.Operation.JAZ,
			0b0011 => Constants.Operation.JANZ,
			_ => Constants.Operation.Invalid,
		};

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			decodeResult.AddError(_decodeErrorName, $"invalid format SB opcode 0x{opcode:X2}");
			return;
		}

		// NOP is truly 1-byte, the others use the upper byte as an immediate
		if (decodeResult.ResultObject.Operation == Constants.Operation.NOP)
		{
			decodeResult.ResultObject.Length = 1;
		}
		else
		{
			uint data = (uint)(decodeResult.ResultObject.InstructionWord >> 8);
			decodeResult.ResultObject.Destination = new()
			{
				DataSize = Constants.DataSize.Byte,
				Data = data,
			};
		}
	}

	private void DecodeFormatBLK(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
		// 9:8 Size
		// 10 Increment
		// 11 Repeat
		// 15:12 Function

		int opcode = (decodeResult.ResultObject.InstructionWord >> 4) & 0b1111;
		int function = (decodeResult.ResultObject.InstructionWord >> 12) & 0b111;
		int operationSelector = (opcode << 3) | function;

		decodeResult.ResultObject.Operation = operationSelector switch
		{
			0b0000_000 => Constants.Operation.BLD,
			0b0000_001 => Constants.Operation.BCP,
			0b0000_010 => Constants.Operation.BTST,
			0b0000_011 => Constants.Operation.BIN,
			0b0000_100 => Constants.Operation.BOUT,
			_ => Constants.Operation.Invalid,
		};

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"invalid format BLK opcode and function 0x{opcode:X2}, 0x{function:X2}"
			);
			return;
		}

		int sizeSelector = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);

		if (size is Constants.DataSize.Dword or Constants.DataSize.Qword)
		{
			decodeResult.AddError(
				_decodeErrorName,
				$"invalid size {size} for operation {decodeResult.ResultObject.Operation}"
			);
			return;
		}

		decodeResult.ResultObject.DataSize = size;

		bool increment = ((decodeResult.ResultObject.InstructionWord >> 10) & 1) != 0;
		bool repeat = ((decodeResult.ResultObject.InstructionWord >> 11) & 1) != 0;

		decodeResult.ResultObject.Increment = increment;
		decodeResult.ResultObject.Repeat = repeat;
	}

	/// <summary>
	/// Decodes a register target from a 3-bit selector
	/// </summary>
	/// <param name="selector"></param>
	/// <param name="size"></param>
	/// <returns></returns>
	/// <exception c="ArgumentException"></exception>
	private static Constants.RegisterTarget DecodeRegister(int selector, Constants.DataSize size)
	{
		if (size is Constants.DataSize.Byte or Constants.DataSize.Word)
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
	/// <exception c="ArgumentException"></exception>
	private static Constants.DataSize DecodeSize(int selector)
	{
		return selector switch
		{
			0b00 => Constants.DataSize.Byte,
			0b01 => Constants.DataSize.Word,
			0b10 => Constants.DataSize.Dword,
			0b11 => Constants.DataSize.Qword,
			_ => throw new ArgumentException($"invalid size selector {selector:X}", nameof(selector)),
		};
	}

	private static Result<Constants.Condition?> DecodeCondition(int selector)
	{
		// Fully out of range
		if (selector is < 0 or > 0b1111)
		{
			throw new ArgumentException($"invalid condition selector {selector:X}", nameof(selector));
		}

		Result<Constants.Condition?> result = new(null);

		switch (selector)
		{
			case 0b0000:
			{
				result.ResultObject = Constants.Condition.NotZero;
				break;
			}
			case 0b0001:
			{
				result.ResultObject = Constants.Condition.Zero;
				break;
			}
			case 0b0010:
			{
				result.ResultObject = Constants.Condition.NoCarry;
				break;
			}
			case 0b0011:
			{
				result.ResultObject = Constants.Condition.Carry;
				break;
			}
			case 0b0100:
			{
				result.ResultObject = Constants.Condition.ParityOdd_NoOverflow;
				break;
			}
			case 0b0101:
			{
				result.ResultObject = Constants.Condition.ParityEven_Overflow;
				break;
			}
			case 0b0110:
			{
				result.ResultObject = Constants.Condition.Plus;
				break;
			}
			case 0b0111:
			{
				result.ResultObject = Constants.Condition.Minus;
				break;
			}
			case 0b1111:
			{
				result.ResultObject = Constants.Condition.Always;
				break;
			}
			default:
			{
				// Invalid but in range, possible in guest
				result.AddError(_decodeErrorName, $"invalid condition selector {selector:X}");
				break;
			}
		}

		return result;
	}

	private Result<MemoryOperation> FetchImmediate(Result<DecodedOperation> decodeResult, Constants.DataSize size)
	{


		Result<MemoryOperation> readResult = _memoryBus.Read(decodeResult.ResultObject.BaseAddress, size);

		// Maybe need to check success before this?

		decodeResult.ResultObject.Length += size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.Dword => 4,
			_ => throw new ArgumentException($"invalid size {size}", nameof(size)),
		};

		return readResult;
	}
}
