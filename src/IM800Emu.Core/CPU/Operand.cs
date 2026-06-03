using System.Text;

namespace IM800Emu.Core.CPU;

public class Operand
{
	public Constants.DataSize DataSize { get; set; }
	public bool Indirect { get; set; }
	public Constants.RegisterTarget Register { get; set; }
	public uint Data { get; set; }
	public short Displacement { get; set; }

	public override string ToString()
	{
		StringBuilder result = new();

		if (Register is not Constants.RegisterTarget.None)
		{
			result.Append(Register.ToString());
		}
		else
		{
			result.Append("0x");
			result.Append(Data.ToString("X"));
		}

		if (Displacement != 0)
		{
			if (Displacement > 0)
			{
				result.Append('+');
			}

			result.Append(Displacement.ToString());
		}

		if (Indirect)
		{
			result.Insert(0, '(');
			result.Append(')');
		}

		return result.ToString();
	}
}
