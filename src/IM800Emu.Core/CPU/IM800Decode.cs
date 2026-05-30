using IM800Emu.Core.Bus;

namespace IM800Emu.Core.CPU;

public partial class IM800
{
	private void DecodeFormatR(ref Result<DecodedOperation> decodedOperation)
	{
		// 1:0 Group
		// 7:2 Opcode
		// 9:8 Size
		// 12:10 Dest register
		// 15:13 Src register

		// Use locals because it's annoying to qualify this every time
		ushort instructionWord = decodedOperation.ResultObject.InstructionWord;
		ref Constants.Operation operation = ref decodedOperation.ResultObject.Operation;
		ref Operand destination = ref decodedOperation.ResultObject.Destination;
		ref Operand source = ref decodedOperation.ResultObject.Source;

		int opcode = (instructionWord >> 2) & 0b111111;
		operation = opcode switch
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

		if (operation == Constants.Operation.Invalid)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException($"invalid format R opcode 0x{opcode:X2}");
			// Eventually we'll have onError debugger callbacks that will have options to continue or return here.
			// For now just return.
			return;
		}

		int sizeSelector = (instructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodedOperation.ResultObject.DataSize = size;

		// Only LEA can use a Qword operand size
		if (size == Constants.DataSize.Qword && operation != Constants.Operation.LEA)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException(
				$"invalid size {size} for operation {operation}"
			);
			return;
		}

		// Instructions in this range use the size field for both operands
		if (opcode < 0b010000)
		{
			destination.DataSize = size;
			source.DataSize = size;
		}
		// LEA uses fixed sizes and reinterprets instruction size
		else if (opcode == 0b010000)
		{
			destination.DataSize = Constants.DataSize.Dword;
			source.DataSize = Constants.DataSize.Word;
		}
		// Bit/shift instructions use byte-sized sources (source is shift amount)
		else
		{
			destination.DataSize = size;
			source.DataSize = Constants.DataSize.Byte;
		}

		int destSelector = (instructionWord >> 10) & 0b111;

		if (destSelector == (int)Constants.RegisterSelector.Immediate)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException($"destination register cannot be immediate");
			return;
		}

		destination.Register = DecodeRegister(destSelector, destination.DataSize);

		int srcSelector = (instructionWord >> 13) & 0b111;

		if (srcSelector == (int)Constants.RegisterSelector.Immediate)
		{
			Result<MemoryOperation> immediateResult = FetchImmediate(ref decodedOperation, source.DataSize);

			if (!immediateResult.IsSuccess)
			{
				decodedOperation.IsSuccess = false;
				decodedOperation.Exception = immediateResult.Exception;
				return;
			}
		}
		else
		{
			source.Register = DecodeRegister(srcSelector, source.DataSize);
		}
	}

	private void DecodeFormatRM(ref Result<DecodedOperation> decodedOperation)
	{
		// 1:0 Group
		// 6:2 Opcode
		// 7 Direction
		// 9:8 Size
		// 12:10 Register
		// 15:13 Address register

		// Use locals because it's annoying to qualify this every time
		ushort instructionWord = decodedOperation.ResultObject.InstructionWord;
		ref Constants.Operation operation = ref decodedOperation.ResultObject.Operation;
		ref Operand destination = ref decodedOperation.ResultObject.Destination;
		ref Operand source = ref decodedOperation.ResultObject.Source;

		int opcode = (instructionWord >> 2) & 0b11111;
		operation = opcode switch
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

		if (operation == Constants.Operation.Invalid)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException($"invalid format RM opcode 0x{opcode:X2}");
			// Eventually we'll have onError debugger callbacks that will have options to continue or return here.
			// For now just return.
			return;
		}

		bool store = ((instructionWord >> 7) & 1) == 1;

		int sizeSelector = (instructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodedOperation.ResultObject.DataSize = size;

		// Only LEA can use a Qword operand size
		if (size == Constants.DataSize.Qword && operation != Constants.Operation.LEA)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException(
				$"invalid size {size} for operation {operation}"
			);
			return;
		}

		// Instructions in this range use the size field for both operands
		if (opcode < 0b10000)
		{
			destination.DataSize = size;
			source.DataSize = size;
		}
		// LEA uses fixed sizes and reinterprets instruction size
		else if (opcode == 0b10000)
		{
			destination.DataSize = Constants.DataSize.Dword;
			source.DataSize = Constants.DataSize.Word;
		}
		// Bit/shift instructions use byte-sized sources (source is shift amount)
		else
		{
			destination.DataSize = size;
			source.DataSize = Constants.DataSize.Byte;
		}

		int registerSelector = (instructionWord >> 10) & 0b111;
		int addressRegisterSelector = (instructionWord >> 12) & 0b111;

		if (registerSelector == (int)Constants.RegisterSelector.Immediate && addressRegisterSelector == (int)Constants.RegisterSelector.Immediate)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException($"cannot use an immediate value for both register and address register");
			return;
		}

		if (store)
		{
			// Address register is the destination
			destination.Indirect = true;

			if (addressRegisterSelector == (int)Constants.RegisterSelector.Immediate)
			{
				Result<MemoryOperation> immediateResult = FetchImmediate(ref decodedOperation, Constants.DataSize.Dword);

				if (!immediateResult.IsSuccess)
				{
					decodedOperation.IsSuccess = false;
					decodedOperation.Exception = immediateResult.Exception;
					return;
				}

				destination.Data = immediateResult.ResultObject.Data;
			}
			else
			{
				destination.Register = DecodeRegister(addressRegisterSelector, Constants.DataSize.Dword);
				if (addressRegisterSelector is (int)Constants.RegisterSelector.IX or (int)Constants.RegisterSelector.IY or (int)Constants.RegisterSelector.SP)
				{
					Result<MemoryOperation> displacementResult = FetchImmediate(ref decodedOperation, Constants.DataSize.Word);

					if (!displacementResult.IsSuccess)
					{
						decodedOperation.IsSuccess = false;
						decodedOperation.Exception = displacementResult.Exception;
						return;
					}

					destination.Displacement = (ushort)displacementResult.ResultObject.Data;
				}
			}

			// Register is the source
			if (registerSelector == (int)Constants.RegisterSelector.Immediate)
			{
				Result<MemoryOperation> immediateResult = FetchImmediate(ref decodedOperation, source.DataSize);

				if (!immediateResult.IsSuccess)
				{
					decodedOperation.IsSuccess = false;
					decodedOperation.Exception = immediateResult.Exception;
					return;
				}

				source.Data = immediateResult.ResultObject.Data;
			}
			else
			{
				source.Register = DecodeRegister(registerSelector, source.DataSize);
			}
		}
		else
		{
			// Address register is the source
			source.Indirect = true;

			if (addressRegisterSelector == (int)Constants.RegisterSelector.Immediate)
			{
				Result<MemoryOperation> immediateResult = FetchImmediate(ref decodedOperation, Constants.DataSize.Dword);

				if (!immediateResult.IsSuccess)
				{
					decodedOperation.IsSuccess = false;
					decodedOperation.Exception = immediateResult.Exception;
					return;
				}

				source.Data = immediateResult.ResultObject.Data;
			}
			else
			{
				source.Register = DecodeRegister(addressRegisterSelector, Constants.DataSize.Dword);
				if (addressRegisterSelector is (int)Constants.RegisterSelector.IX or (int)Constants.RegisterSelector.IY or (int)Constants.RegisterSelector.SP)
				{
					Result<MemoryOperation> displacementResult = FetchImmediate(ref decodedOperation, Constants.DataSize.Word);

					if (!displacementResult.IsSuccess)
					{
						decodedOperation.IsSuccess = false;
						decodedOperation.Exception = displacementResult.Exception;
						return;
					}

					source.Displacement = (ushort)displacementResult.ResultObject.Data;
				}
			}

			// Register is the destination
			if (registerSelector == (int)Constants.RegisterSelector.Immediate)
			{
				Result<MemoryOperation> immediateResult = FetchImmediate(ref decodedOperation, destination.DataSize);

				if (!immediateResult.IsSuccess)
				{
					decodedOperation.IsSuccess = false;
					decodedOperation.Exception = immediateResult.Exception;
					return;
				}

				destination.Data = immediateResult.ResultObject.Data;
			}
			else
			{
				destination.Register = DecodeRegister(registerSelector, source.DataSize);
			}
		}
	}

	private void DecodeFormatUR(ref Result<DecodedOperation> decodedOperation)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
		// 9:8 Size
		// 12:10 Register
		// 15:13 Function

		// Use locals because it's annoying to qualify this every time
		ushort instructionWord = decodedOperation.ResultObject.InstructionWord;
		ref Constants.Operation operation = ref decodedOperation.ResultObject.Operation;
		ref Operand destination = ref decodedOperation.ResultObject.Destination;

		destination.Indirect = true;

		int opcode = (instructionWord >> 4) & 0b1111;
		int function = (instructionWord >> 13) & 0b111;
		int operationSelector = (opcode << 3) | function;

		operation = operationSelector switch
		{
			0b0000_000 => Constants.Operation.PUSH,
			0b0000_001 => Constants.Operation.POP,
			0b0000_010 => Constants.Operation.EXH,
			0b0000_011 => Constants.Operation.EXT,
			0b0000_100 => Constants.Operation.INC,
			0b0000_101 => Constants.Operation.DEC,
			0b0000_110 => Constants.Operation.NEG,
			0b0000_111 => Constants.Operation.CPL,
			0b0001_000 => Constants.Operation.MLT,
			0b0001_001 => Constants.Operation.DIV,
			0b0001_010 => Constants.Operation.SDIV,
			0b0001_011 => Constants.Operation.EX_Alt,
			_ => Constants.Operation.Invalid,
		};

		if (operation == Constants.Operation.Invalid)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException($"invalid format UR opcode and function 0x{opcode:X2}, 0x{function:X2}");
			// Eventually we'll have onError debugger callbacks that will have options to continue or return here.
			// For now just return.
			return;
		}

		int sizeSelector = (instructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodedOperation.ResultObject.DataSize = size;
		destination.DataSize = size;

		if (size == Constants.DataSize.Qword)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException($"invalid size {size} for operation {operation}");
			return;
		}

		int registerSelector = (instructionWord >> 10) & 0b111;

		if (registerSelector == (int)Constants.RegisterSelector.Immediate)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException($"Format UR cannot use an immediate value");
			return;
		}

		destination.Register = DecodeRegister(registerSelector, destination.DataSize);
	}

	private void DecodeFormatUM(ref Result<DecodedOperation> decodedOperation)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
		// 9:8 Size
		// 12:10 Function
		// 15:13 Address register

		// Use locals because it's annoying to qualify this every time
		ushort instructionWord = decodedOperation.ResultObject.InstructionWord;
		ref Constants.Operation operation = ref decodedOperation.ResultObject.Operation;
		ref Operand destination = ref decodedOperation.ResultObject.Destination;

		destination.Indirect = true;

		int opcode = (instructionWord >> 4) & 0b1111;
		int function = (instructionWord >> 10) & 0b111;
		int operationSelector = (opcode << 3) | function;

		operation = operationSelector switch
		{
			0b0000_000 => Constants.Operation.PUSH,
			0b0000_001 => Constants.Operation.POP,
			0b0000_010 => Constants.Operation.EXH,
			0b0000_011 => Constants.Operation.EXT,
			0b0000_100 => Constants.Operation.INC,
			0b0000_101 => Constants.Operation.DEC,
			0b0000_110 => Constants.Operation.NEG,
			0b0000_111 => Constants.Operation.CPL,
			0b0001_000 => Constants.Operation.MLT,
			0b0001_001 => Constants.Operation.DIV,
			0b0001_010 => Constants.Operation.SDIV,
			0b0001_011 => Constants.Operation.EX_Alt,
			_ => Constants.Operation.Invalid,
		};

		if (operation == Constants.Operation.Invalid)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException($"invalid format UR opcode and function 0x{opcode:X2}, 0x{function:X2}");
			// Eventually we'll have onError debugger callbacks that will have options to continue or return here.
			// For now just return.
			return;
		}

		int sizeSelector = (instructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodedOperation.ResultObject.DataSize = size;
		destination.DataSize = size;

		if (size == Constants.DataSize.Qword)
		{
			decodedOperation.IsSuccess = false;
			decodedOperation.Exception = new InvalidOperationException($"invalid size {size} for operation {operation}");
			return;
		}

		int addressRegisterSelector = (instructionWord >> 13) & 0b111;

		if (addressRegisterSelector == (int)Constants.RegisterSelector.Immediate)
		{
			Result<MemoryOperation> immediateResult = FetchImmediate(ref decodedOperation, Constants.DataSize.Dword);

			if (!immediateResult.IsSuccess)
			{
				decodedOperation.IsSuccess = false;
				decodedOperation.Exception = immediateResult.Exception;
				return;
			}

			destination.Data = immediateResult.ResultObject.Data;
		}
		else
		{
			destination.Register = DecodeRegister(addressRegisterSelector, Constants.DataSize.Dword);
			if (addressRegisterSelector is (int)Constants.RegisterSelector.IX or (int)Constants.RegisterSelector.IY or (int)Constants.RegisterSelector.SP)
			{
				Result<MemoryOperation> displacementResult = FetchImmediate(ref decodedOperation, Constants.DataSize.Word);

				if (!displacementResult.IsSuccess)
				{
					decodedOperation.IsSuccess = false;
					decodedOperation.Exception = displacementResult.Exception;
					return;
				}

				destination.Displacement = (ushort)displacementResult.ResultObject.Data;
			}
		}
	}

	private void DecodeFormatB(ref Result<DecodedOperation> result)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 8:4 Opcode
		// 12:9 Condition
		// 15:13 Address register
	}

	private void DecodeFormatM(ref Result<DecodedOperation> result)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
		// 15:8 Function
	}

	private void DecodeFormatSB(ref Result<DecodedOperation> result)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
	}

	private void DecodeFormatBlock(ref Result<DecodedOperation> result)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
		// 9:8 Size
		// 10 Increment
		// 11 Repeat
		// 15:12 Function
	}

	/// <summary>
	/// Decodes a register target from a 3-bit selector
	/// </summary>
	/// <param name="selector"></param>
	/// <param name="size"></param>
	/// <returns></returns>
	/// <exception cref="ArgumentException"></exception>
	private Constants.RegisterTarget DecodeRegister(int selector, Constants.DataSize size)
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
	/// <exception cref="ArgumentException"></exception>
	private Constants.DataSize DecodeSize(int selector)
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

	private Result<MemoryOperation> FetchImmediate(ref Result<DecodedOperation> result, Constants.DataSize size)
	{
		Result<MemoryOperation> readResult = _memoryBus.Read(result.ResultObject.BaseAddress, size);

		if (!readResult.IsSuccess)
		{
			result.IsSuccess = false;
			result.Exception = readResult.Exception;
			return readResult;
		}

		result.ResultObject.Length += size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.Dword => 4,
			_ => throw new ArgumentException($"invalid size {size}", nameof(size)),
		};

		return readResult;
	}
}
