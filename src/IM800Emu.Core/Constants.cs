namespace IM800Emu.Core;

public static class Constants
{
	public enum Condition
	{
		Always = 0,
		NotZero,
		Zero,
		NoCarry,
		Carry,
		ParityOdd_NoOverflow,
		ParityEven_Overflow,
		Plus,
		Minus,
		Less,
		GreaterEqual
	}

	public enum DataSize
	{
		None = 0,
		Byte,
		Word,
		Dword,
		Qword
	}

	public enum FlagMask : ushort
	{
		// Use flag bits exclusively since it's a pain to maintain separate flags and IFF1/2 in software.
		// Technically EI should be a view of IFF1, and bit 9 is reserved and must be maintained by software.
		EnableInterruptsSave = 0b0000_0010_0000_0000,
		EnableInterrupts = 0b0000_0001_0000_0000,
		Sign = 0b1000_0000,
		Zero = 0b0100_0000,
		Unused5 = 0b0010_0000,
		HalfCarry = 0b0001_0000,
		Less = 0b0000_1000,
		ParityOverflow = 0b0000_0100,
		Subtract = 0b0000_0010,
		Carry = 0b0000_0001
	}

	public enum Operation
	{
		Invalid = 0,
		Halted,
		Interrupted,
		NonMaskableInterrupt,
		LD,
		EX,
		PUSH,
		POP,
		EXH,
		LEA,
		EXA,
		EXX,
		EXI,
		IN_OUT,
		ADD,
		ADC,
		SUB,
		SBC,
		CP,
		INC,
		DEC,
		NEG,
		EXT,
		MLT,
		DIV,
		SDIV,
		DAA,
		AND,
		OR,
		XOR,
		TST,
		CPL,
		BIT,
		SET,
		RES,
		RLC,
		RRC,
		RL,
		RR,
		SLA,
		SRA,
		SRL,
		RLD,
		RRD,
		NOP,
		JP,
		JR,
		DJNZ,
		JAZ,
		JANZ,
		CALL,
		CR,
		RET,
		RETI,
		RETN,
		RST,
		SCF,
		CCF,
		EI,
		DI,
		IM1,
		IM2,
		HALT,
		LDI,
		LDAR,
		LDRA,
		BLD,
		BCP,
		BTST,
		BIN,
		BOUT,
		BKPT
	}

	public enum RegisterSelector
	{
		A = 0b000,
		B = 0b001,
		C = 0b010,
		D = 0b011,
		E = 0b100,
		H = 0b101,
		L = 0b110,
		Immediate = 0b111,
		AF = 0b000,
		BC = 0b001,
		DE = 0b010,
		HL = 0b011,
		IX = 0b100,
		IY = 0b101,
		SP = 0b110
	}

	public enum RegisterTarget
	{
		None = 0,
		A, F, B, C, D, E, H, L,
		AF, BC, DE, HL, IX, IY, SP,
		A_, F_, B_, C_, D_, E_, H_, L_,
		AF_, BC_, DE_, HL_, IX_, IY_, SP_,
		PC, I, R
	}

	public enum SymbolType
	{
		Label,
		EQU
	}

	public static readonly int DwordALUCost = 1;
	public static readonly int CpuSpeedHz = 4000000;
	public static readonly int TargetFramerate = 60;
}
