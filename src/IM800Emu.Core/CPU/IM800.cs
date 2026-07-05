using System.Diagnostics;
using IM800Emu.Core.Bus;

namespace IM800Emu.Core.CPU;

/// <summary>
/// Implements the IM800 ISA. Split into several files: IM800.cs, IM800Decode.cs, and IM800Execute.cs
/// </summary>
public partial class IM800
{
	private readonly Registers _registers;
	private readonly MemoryBus _memoryBus;
	private static readonly string _decodeErrorName = "Decode";
	private static readonly string _executeErrorName = "Execute";

	public Registers Registers => _registers;

	public IM800(MemoryBus memoryBus)
	{
		_registers = new();
		_memoryBus = memoryBus;
	}

	/// <summary>
	/// Decodes the next operation to execute. Includes interrupts, halt states, and the instruction at PC.
	/// </summary>
	/// <returns></returns>
	public Result<DecodedOperation> Decode()
	{
		uint pc = _registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		return DecodeAt(pc);
	}

	/// <summary>
	/// Decodes the data at a given address as an instruction.
	/// </summary>
	/// <param name="baseAddress"></param>
	/// <returns></returns>
	public Result<DecodedOperation> DecodeAt(uint baseAddress)
	{
		DecodedOperation resultObject = new()
		{
			BaseAddress = baseAddress,
			Length = 2,
		};
		Result<DecodedOperation> decodeResult = new(resultObject);

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
						decodeResult.AddError(
							_decodeErrorName,
							$"invalid special subgroup selector 0x{subgroupSelector:X2}"
						);
						break;
					}
				}
				break;
			}
		}

		return decodeResult;
	}

	/// <summary>
	/// Executes a decoded operation. Expects the operation to come from Decode, and may produce incorrect results when
	/// used with another operation.
	/// </summary>
	/// <param name="operation"></param>
	/// <returns>A result with the number of cycles taken to execute</returns>
	public Result<int> Execute(DecodedOperation operation)
	{
		uint pc = _registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		pc += operation.Length;
		_registers.Write(Constants.RegisterTarget.PC, Constants.DataSize.Dword, pc);

		uint r = _registers.Read(Constants.RegisterTarget.R, Constants.DataSize.Word);
		r++;
		_registers.Write(Constants.RegisterTarget.R, Constants.DataSize.Word, r);

		Result<int> executeResult = operation.Operation switch
		{
			Constants.Operation.Invalid => ExecuteInvalid(operation),
			Constants.Operation.Halted => ExecuteHalted(operation),
			Constants.Operation.Interrupted => ExecuteInterrupted(operation),
			Constants.Operation.LD => ExecuteLD(operation),
			Constants.Operation.EX => ExecuteEX(operation),
			Constants.Operation.PUSH => ExecutePUSH(operation),
			Constants.Operation.POP => ExecutePOP(operation),
			Constants.Operation.EXH => ExecuteEXH(operation),
			Constants.Operation.LEA => ExecuteESA(operation),
			Constants.Operation.EXA => ExecuteEXA(operation),
			Constants.Operation.EXX => ExecuteEXX(operation),
			Constants.Operation.EXI => ExecuteEXI(operation),
			Constants.Operation.IN_OUT => ExecuteIN_OUT(operation),
			Constants.Operation.ADD => ExecuteADD(operation),
			Constants.Operation.ADC => ExecuteADC(operation),
			Constants.Operation.SUB => ExecuteSUB(operation),
			Constants.Operation.SBC => ExecuteSBC(operation),
			Constants.Operation.CP => ExecuteCP(operation),
			Constants.Operation.INC => ExecuteINC(operation),
			Constants.Operation.DEC => ExecuteDEC(operation),
			Constants.Operation.NEG => ExecuteNEG(operation),
			Constants.Operation.EXT => ExecuteEXT(operation),
			Constants.Operation.MLT => ExecuteMLT(operation),
			Constants.Operation.DIV => ExecuteDIV(operation),
			Constants.Operation.SDIV => ExecuteSDIV(operation),
			Constants.Operation.DAA => ExecuteDAA(operation),
			Constants.Operation.AND => ExecuteAND(operation),
			Constants.Operation.OR => ExecuteOR(operation),
			Constants.Operation.XOR => ExecuteXOR(operation),
			Constants.Operation.TST => ExecuteTST(operation),
			Constants.Operation.CPL => ExecuteCPL(operation),
			Constants.Operation.BIT => ExecuteBIT(operation),
			Constants.Operation.SET => ExecuteSET(operation),
			Constants.Operation.RES => ExecuteRES(operation),
			Constants.Operation.RLC => ExecuteRLC(operation),
			Constants.Operation.RRC => ExecuteRRC(operation),
			Constants.Operation.RL => ExecuteRL(operation),
			Constants.Operation.RR => ExecuteRR(operation),
			Constants.Operation.SLA => ExecuteSLA(operation),
			Constants.Operation.SRA => ExecuteSRA(operation),
			Constants.Operation.SRL => ExecuteSRL(operation),
			Constants.Operation.RLD => ExecuteRLD(operation),
			Constants.Operation.RRD => ExecuteRRD(operation),
			Constants.Operation.NOP => ExecuteNOP(operation),
			Constants.Operation.JP => ExecuteJP(operation),
			Constants.Operation.JR => ExecuteJR(operation),
			Constants.Operation.DJNZ => ExecuteDJNZ(operation),
			Constants.Operation.JAZ => ExecuteJAZ(operation),
			Constants.Operation.JANZ => ExecuteJANZ(operation),
			Constants.Operation.CALL => ExecuteCALL(operation),
			Constants.Operation.CR => ExecuteCR(operation),
			Constants.Operation.RET => ExecuteRET(operation),
			Constants.Operation.RETI => ExecuteRETI(operation),
			Constants.Operation.RETN => ExecuteRETN(operation),
			Constants.Operation.RST => ExecuteRST(operation),
			Constants.Operation.SCF => ExecuteSCF(operation),
			Constants.Operation.CCF => ExecuteCCF(operation),
			Constants.Operation.EI => ExecuteEI(operation),
			Constants.Operation.DI => ExecuteDI(operation),
			Constants.Operation.IM1 => ExecuteIM1(operation),
			Constants.Operation.IM2 => ExecuteIM2(operation),
			Constants.Operation.HALT => ExecuteHALT(operation),
			Constants.Operation.LDI => ExecuteLDI(operation),
			Constants.Operation.LDAR => ExecuteLDAR(operation),
			Constants.Operation.LDRA => ExecuteLDRA(operation),
			Constants.Operation.BLD => ExecuteBLD(operation),
			Constants.Operation.BCP => ExecuteBCP(operation),
			Constants.Operation.BTST => ExecuteBTST(operation),
			Constants.Operation.BIN => ExecuteBIN(operation),
			Constants.Operation.BOUT => ExecuteBOUT(operation),
			_ => throw new NotImplementedException($"Execute{operation.Operation} does not exist"),
		};

		return executeResult;
	}
}
