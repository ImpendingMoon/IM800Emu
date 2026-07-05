namespace IM800Emu.Core.CPU;

public partial class IM800
{
	private bool IsConditionTrue(Constants.Condition condition)
	{
		return condition switch
		{
			Constants.Condition.NotZero => !Registers.GetFlag(Constants.FlagMask.Zero),
			Constants.Condition.Zero => Registers.GetFlag(Constants.FlagMask.Zero),
			Constants.Condition.NoCarry => !Registers.GetFlag(Constants.FlagMask.Carry),
			Constants.Condition.Carry => Registers.GetFlag(Constants.FlagMask.Carry),
			Constants.Condition.ParityOdd_NoOverflow => !Registers.GetFlag(Constants.FlagMask.ParityOverflow),
			Constants.Condition.ParityEven_Overflow => Registers.GetFlag(Constants.FlagMask.ParityOverflow),
			Constants.Condition.Plus => !Registers.GetFlag(Constants.FlagMask.Sign),
			Constants.Condition.Minus => Registers.GetFlag(Constants.FlagMask.Sign),
			Constants.Condition.Always => true,
			_ => throw new InvalidOperationException($"invalid condition {condition}"),
		};
	}

	private ALUFlagState GetALUFlags()
	{
		return new ALUFlagState
		{
			Carry = Registers.GetFlag(Constants.FlagMask.Carry),
			Subtract = Registers.GetFlag(Constants.FlagMask.Subtract),
			ParityOverflow = Registers.GetFlag(Constants.FlagMask.ParityOverflow),
			HalfCarry = Registers.GetFlag(Constants.FlagMask.HalfCarry),
			Zero = Registers.GetFlag(Constants.FlagMask.Zero),
			Sign = Registers.GetFlag(Constants.FlagMask.Sign),
		};
	}

	private void UpdateALUFlags(ALUFlagState state)
	{
		Registers.SetFlag(Constants.FlagMask.Carry, state.Carry);
		Registers.SetFlag(Constants.FlagMask.Subtract, state.Subtract);
		Registers.SetFlag(Constants.FlagMask.ParityOverflow, state.ParityOverflow);
		Registers.SetFlag(Constants.FlagMask.HalfCarry, state.HalfCarry);
		Registers.SetFlag(Constants.FlagMask.Zero, state.Zero);
		Registers.SetFlag(Constants.FlagMask.Sign, state.Sign);
	}

	private struct ALUFlagState
	{
		public bool Carry;
		public bool Subtract;
		public bool ParityOverflow;
		public bool HalfCarry;
		public bool Zero;
		public bool Sign;
	}
}