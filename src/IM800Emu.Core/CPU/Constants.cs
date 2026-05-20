namespace IM800Emu.Core.CPU;

public static class Constants
{
	public enum Operation
	{
		Invalid, Halted, Interrupted,
		LD, EX, PUSH,
		POP, EXH, LEA,
		EX_Alt, EXX, EXI,
		IN, OUT, ADD,
		ADC, SUB, SBC,
		CP, INC, DEC,
		NEG, EXT, MLT,
		DIV, SDIV, DAA,
		AND, OR, XOR,
		TST, CPL, BIT,
		SET, RES, RLC,
		RRC, RL, RR,
		SLA, SRA, SRL,
		RLD, RRD, NOP,
		JP, JR, DJNZ,
		JAZ, JANZ, CALL,
		CR, RET, RETI,
		RETN, RST, SCF,
		CCF, EI, DI,
		IM1, IM2, HALT,
		LDI, LDAR, LDRA,
		BLD, BCP, BTST,
		BIN, BOUT,
	}

	public enum DataSize
	{
		Byte,
		Word,
		Dword,
		Qword,
	}

	public enum RegisterTarget
	{
		A, F, B, C, D, E, H, L,
		AF, BC, DE, HL, IX, IY, SP,
		A_, F_, B_, C_, D_, E_, H_, L_,
		AF_, BC_, DE_, HL_, IX_, IY_, SP_,
		I, R, IFF1, IFF2,
	}
}
