using IM800Emu.Core.Bus;
using System.Diagnostics;

namespace IM800Emu.Core.CPU;

public partial class IM800
{
	private void DecodeFormatR(Result<DecodedOperation> decodeResult)
	{
		Debug.Assert(decodeResult.ResultObject is not null);

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
			var exception = new InvalidOperationException($"invalid format R opcode 0x{opcode:X2}");
			decodeResult.Exceptions.Add(exception);
			return;
		}

		int sizeSelector = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodeResult.ResultObject.DataSize = size;

		// Only LEA can use a Qword operand size
		if (size == Constants.DataSize.Qword && decodeResult.ResultObject.Operation != Constants.Operation.LEA)
		{
			var exception = new InvalidOperationException(
				$"invalid size {size} for decodeResult.ResultObject.Operation {decodeResult.ResultObject.Operation}"
			);
			decodeResult.Exceptions.Add(exception);
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

		int destSelector = (decodeResult.ResultObject.InstructionWord >> 10) & 0b111;

		if (destSelector == (int)Constants.RegisterSelector.Immediate)
		{
			var exception = new InvalidOperationException($"destination register cannot be immediate");

			decodeResult.Exceptions.Add(exception);
			return;
		}

		decodeResult.ResultObject.Destination.Register = DecodeRegister(
			destSelector,
			decodeResult.ResultObject.Destination.DataSize
		);

		int srcSelector = (decodeResult.ResultObject.InstructionWord >> 13) & 0b111;

		if (srcSelector == (int)Constants.RegisterSelector.Immediate)
		{
			Result<MemoryOperation> immediateResult = FetchImmediate(
				decodeResult,
				decodeResult.ResultObject.Source.DataSize
			);

			if (!immediateResult.IsSuccess)
			{
				decodeResult.Exceptions.AddRange(immediateResult.Exceptions);
				return;
			}
		}
		else
		{
			decodeResult.ResultObject.Source.Register = DecodeRegister(
				srcSelector,
				decodeResult.ResultObject.Source.DataSize
			);
		}
	}

	private void DecodeFormatRM(Result<DecodedOperation> decodeResult)
	{
		Debug.Assert(decodeResult.ResultObject is not null);

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
			var exception = new InvalidOperationException($"invalid format RM opcode 0x{opcode:X2}");
			decodeResult.Exceptions.Add(exception);
			return;
		}

		bool store = ((decodeResult.ResultObject.InstructionWord >> 7) & 1) == 1;

		int sizeSelector = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodeResult.ResultObject.DataSize = size;

		// Only LEA can use a Qword operand size
		if (size == Constants.DataSize.Qword && decodeResult.ResultObject.Operation != Constants.Operation.LEA)
		{
			var exception = new InvalidOperationException(
				$"invalid size {size} for decodeResult.ResultObject.Operation {decodeResult.ResultObject.Operation}"
			);
			decodeResult.Exceptions.Add(exception);
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
		// Bit/shift instructions use byte-sized sources (decodeResult.ResultObject.Source is shift amount)
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
			var exception = new InvalidOperationException(
				$"cannot use an immediate value for both register and address register"
			);
			decodeResult.Exceptions.Add(exception);
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
					decodeResult.Exceptions.AddRange(immediateResult.Exceptions);
					return;
				}

				decodeResult.ResultObject.Destination.Data = immediateResult.ResultObject!.Data;
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
						decodeResult.Exceptions.AddRange(displacementResult.Exceptions);
						return;
					}

					decodeResult.ResultObject.Destination.Displacement = (ushort)displacementResult.ResultObject!.Data;
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
					decodeResult.Exceptions.AddRange(immediateResult.Exceptions);
					return;
				}

				decodeResult.ResultObject.Source.Data = immediateResult.ResultObject!.Data;
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
					decodeResult.Exceptions.AddRange(immediateResult.Exceptions);
					return;
				}

				decodeResult.ResultObject.Source.Data = immediateResult.ResultObject!.Data;
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
						decodeResult.Exceptions.AddRange(displacementResult.Exceptions);
						return;
					}

					decodeResult.ResultObject.Source.Displacement = (ushort)displacementResult.ResultObject!.Data;
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
					decodeResult.Exceptions.AddRange(immediateResult.Exceptions);
					return;
				}

				decodeResult.ResultObject.Destination.Data = immediateResult.ResultObject!.Data;
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
		Debug.Assert(decodeResult.ResultObject is not null);

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

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			var exception = new InvalidOperationException(
				$"invalid format UR opcode and function 0x{opcode:X2}, 0x{function:X2}"
			);
			decodeResult.Exceptions.Add(exception);
			return;
		}

		int sizeSelector = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodeResult.ResultObject.DataSize = size;
		decodeResult.ResultObject.Destination.DataSize = size;

		if (size == Constants.DataSize.Qword)
		{
			var exception = new InvalidOperationException(
				$"invalid size {size} for decodeResult.ResultObject.Operation {decodeResult.ResultObject.Operation}"
			);
			decodeResult.Exceptions.Add(exception);
			return;
		}

		int registerSelector = (decodeResult.ResultObject.InstructionWord >> 10) & 0b111;

		if (registerSelector == (int)Constants.RegisterSelector.Immediate)
		{
			var exception = new InvalidOperationException($"Format UR cannot use an immediate value");
			decodeResult.Exceptions.Add(exception);
			return;
		}

		decodeResult.ResultObject.Destination.Register = DecodeRegister(
			registerSelector,
			decodeResult.ResultObject.Destination.DataSize
		);
	}

	private void DecodeFormatUM(Result<DecodedOperation> decodeResult)
	{
		Debug.Assert(decodeResult.ResultObject is not null);

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

		if (decodeResult.ResultObject.Operation == Constants.Operation.Invalid)
		{
			var exception = new InvalidOperationException(
				$"invalid format UR opcode and function 0x{opcode:X2}, 0x{function:X2}"
			);
			decodeResult.Exceptions.Add(exception);
			return;
		}

		int sizeSelector = (decodeResult.ResultObject.InstructionWord >> 8) & 0b11;
		Constants.DataSize size = DecodeSize(sizeSelector);
		decodeResult.ResultObject.DataSize = size;
		decodeResult.ResultObject.Destination.DataSize = size;

		if (size == Constants.DataSize.Qword)
		{
			var exception = new InvalidOperationException(
				$"invalid size {size} for decodeResult.ResultObject.Operation {decodeResult.ResultObject.Operation}"
			);
			decodeResult.Exceptions.Add(exception);
			return;
		}

		int addressRegisterSelector = (decodeResult.ResultObject.InstructionWord >> 13) & 0b111;

		if (addressRegisterSelector == (int)Constants.RegisterSelector.Immediate)
		{
			Result<MemoryOperation> immediateResult = FetchImmediate(decodeResult, Constants.DataSize.Dword);

			if (!immediateResult.IsSuccess)
			{
				decodeResult.Exceptions.AddRange(immediateResult.Exceptions);
				return;
			}

			decodeResult.ResultObject.Destination.Data = immediateResult.ResultObject!.Data;
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
					decodeResult.Exceptions.AddRange(displacementResult.Exceptions);
					return;
				}

				decodeResult.ResultObject.Destination.Displacement = (ushort)displacementResult.ResultObject!.Data;
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
	}

	private void DecodeFormatM(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
		// 15:8 Function
	}

	private void DecodeFormatSB(Result<DecodedOperation> decodeResult)
	{
		// 1:0 Group
		// 3:2 Subgroup
		// 7:4 Opcode
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

	private Result<MemoryOperation> FetchImmediate(Result<DecodedOperation> decodeResult, Constants.DataSize size)
	{
		Debug.Assert(decodeResult.ResultObject is not null);

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
