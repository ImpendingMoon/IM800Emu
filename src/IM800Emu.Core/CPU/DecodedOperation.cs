using System.Text;

namespace IM800Emu.Core.CPU;

/// <summary>
/// Represents a fully decoded instruction, ready to be executed
/// </summary>
public class DecodedOperation
{
	public Constants.Operation Operation { get; set; }
	public Operand? Destination { get; set; }
	public Operand? Source { get; set; }
	public int FetchCycles { get; set; }
	public uint BaseAddress { get; set; }
	public uint Length { get; set; }
	public ushort InstructionWord { get; set; }

	public Constants.DataSize DataSize { get; set; }
	// This is only meaningful for branch instructions
	public Constants.Condition Condition { get; set; }
	// These are only meaningful for block instructions
	public bool Increment { get; set; }
	public bool Repeat { get; set; }

	public override string ToString()
	{
		// This will be moved to a dedicated disassembler class when I start on the debugger
		// It's hard making syntactically-correct assembly
		StringBuilder result = new();

		string destination = Destination?.ToString() ?? string.Empty;
		string source = Source?.ToString() ?? string.Empty;

		string mnemonic = Operation switch
		{
			Constants.Operation.LDI => "LD I,",
			Constants.Operation.LDAR => "LD A, R",
			Constants.Operation.LDRA => "LD R, A",
			_ => Operation.ToString(),
		};

		result.Append(mnemonic);

		string size = DataSize switch
		{
			Constants.DataSize.Byte => ".B",
			Constants.DataSize.Word => ".W",
			Constants.DataSize.Dword => ".D",
			_ => string.Empty,
		};

		if (!string.IsNullOrEmpty(size) && Operation is not Constants.Operation.LEA)
		{
			result.Append(size);
		}

		if (Operation
			is Constants.Operation.JR
			or Constants.Operation.JP
			or Constants.Operation.CALL
			or Constants.Operation.CR
			or Constants.Operation.RET
		)
		{
			string condition = Condition switch
			{
				Constants.Condition.Always => string.Empty,
				Constants.Condition.NotZero => "NZ",
				Constants.Condition.Zero => "Z",
				Constants.Condition.NoCarry => "NC",
				Constants.Condition.Carry => "C",
				Constants.Condition.ParityOdd_NoOverflow => "PO",
				Constants.Condition.ParityEven_Overflow => "PE",
				Constants.Condition.Plus => "P",
				Constants.Condition.Minus => "N",
				_ => Condition.ToString(),
			};

			if (!string.IsNullOrEmpty(condition))
			{
				result.Append(' ');
				result.Append(condition);

				if (!string.IsNullOrEmpty(destination))
				{
					result.Append(',');
				}
			}
		}

		if (Operation
			is Constants.Operation.BLD
			or Constants.Operation.BCP
			or Constants.Operation.BTST
			or Constants.Operation.BIN
			or Constants.Operation.BOUT
		)
		{
			result.Append(' ');
			if (Increment)
			{
				result.Append("I, ");
			}
			else
			{
				result.Append("D, ");
			}

			if (Repeat)
			{
				result.Append('R');
			}
			else
			{
				result.Append('S');
			}
		}
		else
		{
			if (!string.IsNullOrEmpty(destination))
			{
				result.Append(' ');
				result.Append(destination);
			}

			if (!string.IsNullOrEmpty(source))
			{
				result.Append(", ");
				result.Append(source);
			}

			if (Operation == Constants.Operation.LEA)
			{
				result.Append(", ");
				result.Append(DataSize.ToString().ToUpperInvariant());
			}
		}

		return result.ToString();
	}
}
