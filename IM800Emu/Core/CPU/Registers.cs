using System.Buffers.Binary;
using System.Text;

namespace IM800Emu.Core.CPU;

/// <summary>
/// </summary>
public class Registers
{
	// Register file size:
	// AF+BC+DE+HL+IX+IY+SP @ 4-bytes = 28 bytes
	// + alts = 56 bytes
	// + PC = 60 bytes
	// + I = 64 bytes
	// + R = 66 bytes
	private readonly byte[] _data = new byte[66];

	public uint Read(Constants.RegisterTarget register, Constants.DataSize size)
	{
		int index = ReadIndex(register);
		return size switch
		{
			Constants.DataSize.Byte => _data[index],
			Constants.DataSize.Word => BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(index)),
			Constants.DataSize.Dword => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(index)),
			_ => throw new ArgumentException($"invalid register size {size}", nameof(size))
		};
	}

	public void Write(Constants.RegisterTarget register, Constants.DataSize size, uint value)
	{
		int index = ReadIndex(register);
		switch (size)
		{
			case Constants.DataSize.Byte:
				_data[index] = (byte)value;
				break;
			case Constants.DataSize.Word:
				BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(index), (ushort)value);
				break;
			case Constants.DataSize.Dword:
				BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(index), value);
				break;
			default:
				throw new ArgumentException($"invalid register size {size}", nameof(size));
		}
	}

	public void ExchangeWithAlternate(Constants.RegisterTarget register, Constants.DataSize size)
	{
		Constants.RegisterTarget alt = GetAlternateTarget(register);

		uint oldPrimary = Read(register, size);
		uint oldAlternate = Read(alt, size);

		Write(register, size, oldAlternate);
		Write(alt, size, oldPrimary);
	}

	public bool GetFlag(Constants.FlagMask flag)
	{
		ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(0));
		return (flags & (ushort)flag) != 0;
	}

	public void SetFlag(Constants.FlagMask flag, bool value)
	{
		ushort flags = BinaryPrimitives.ReadUInt16LittleEndian(_data.AsSpan(0));

		if (value)
		{
			flags |= (ushort)flag;
		}
		else
		{
			flags &= (ushort)~flag;
		}

		BinaryPrimitives.WriteUInt16LittleEndian(_data.AsSpan(0), flags);
	}

	public void ClearRegisters()
	{
		Array.Clear(_data);
	}

	public string GetStandardDisplayString()
	{
		StringBuilder sb = new();

		sb.Append($"AF: {Read(Constants.RegisterTarget.AF, Constants.DataSize.Dword):X8} ");
		sb.Append($"BC: {Read(Constants.RegisterTarget.BC, Constants.DataSize.Dword):X8} ");
		sb.Append($"DE: {Read(Constants.RegisterTarget.DE, Constants.DataSize.Dword):X8} ");
		sb.Append($"HL: {Read(Constants.RegisterTarget.HL, Constants.DataSize.Dword):X8} ");
		sb.Append($"IX: {Read(Constants.RegisterTarget.IX, Constants.DataSize.Dword):X8} ");
		sb.Append($"IY: {Read(Constants.RegisterTarget.IY, Constants.DataSize.Dword):X8} ");
		sb.Append($"SP: {Read(Constants.RegisterTarget.SP, Constants.DataSize.Dword):X8} ");
		sb.Append($"PC: {Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword):X8} ");

		return sb.ToString();
	}

	public string GetAlternateDisplayString()
	{
		var sb = new StringBuilder();

		sb.Append($"AF': {Read(Constants.RegisterTarget.AF_, Constants.DataSize.Dword):X8} ");
		sb.Append($"BC': {Read(Constants.RegisterTarget.BC_, Constants.DataSize.Dword):X8} ");
		sb.Append($"DE': {Read(Constants.RegisterTarget.DE_, Constants.DataSize.Dword):X8} ");
		sb.Append($"HL': {Read(Constants.RegisterTarget.HL_, Constants.DataSize.Dword):X8} ");
		sb.Append($"IX': {Read(Constants.RegisterTarget.IX_, Constants.DataSize.Dword):X8} ");
		sb.Append($"IY': {Read(Constants.RegisterTarget.IY_, Constants.DataSize.Dword):X8} ");
		sb.Append($"SP': {Read(Constants.RegisterTarget.SP_, Constants.DataSize.Dword):X8} ");

		return sb.ToString();
	}

	public string GetSystemDisplayString()
	{
		var sb = new StringBuilder();

		sb.Append($"I: {Read(Constants.RegisterTarget.I, Constants.DataSize.Dword):X8} ");
		sb.Append($"R: {Read(Constants.RegisterTarget.R, Constants.DataSize.Word):X4} ");

		return sb.ToString();
	}

	public string GetFlagsDisplayString()
	{
		var sb = new StringBuilder();

		sb.Append($"C: {GetFlag(Constants.FlagMask.Carry)} ");
		sb.Append($"N: {GetFlag(Constants.FlagMask.Subtract)} ");
		sb.Append($"PV: {GetFlag(Constants.FlagMask.ParityOverflow)} ");
		sb.Append($"Less: {GetFlag(Constants.FlagMask.Less)}");
		sb.Append($"H: {GetFlag(Constants.FlagMask.HalfCarry)} ");
		sb.Append($"Z: {GetFlag(Constants.FlagMask.Zero)} ");
		sb.Append($"S: {GetFlag(Constants.FlagMask.Sign)} ");
		sb.Append($"IE: {GetFlag(Constants.FlagMask.EnableInterrupts)} ");
		sb.Append($"IFF2: {GetFlag(Constants.FlagMask.EnableInterruptsSave)}");

		return sb.ToString();
	}

	public string GetFullDisplayString()
	{
		var sb = new StringBuilder();

		sb.AppendLine("Registers:");
		sb.AppendLine(GetStandardDisplayString());
		sb.AppendLine("Alternate Registers:");
		sb.AppendLine(GetAlternateDisplayString());
		sb.AppendLine("System Registers:");
		sb.AppendLine(GetSystemDisplayString());
		sb.AppendLine("Flags:");
		sb.AppendLine(GetFlagsDisplayString());

		return sb.ToString();
	}

	public override string ToString()
	{
		return GetStandardDisplayString();
	}

	private static int ReadIndex(Constants.RegisterTarget register)
	{
		return register switch
		{
			Constants.RegisterTarget.F => 0,
			Constants.RegisterTarget.A => 2,
			Constants.RegisterTarget.C => 4,
			Constants.RegisterTarget.B => 6,
			Constants.RegisterTarget.E => 8,
			Constants.RegisterTarget.D => 10,
			Constants.RegisterTarget.L => 12,
			Constants.RegisterTarget.H => 14,
			Constants.RegisterTarget.AF => 0,
			Constants.RegisterTarget.BC => 4,
			Constants.RegisterTarget.DE => 8,
			Constants.RegisterTarget.HL => 12,
			Constants.RegisterTarget.IX => 16,
			Constants.RegisterTarget.IY => 20,
			Constants.RegisterTarget.SP => 24,
			Constants.RegisterTarget.F_ => 28,
			Constants.RegisterTarget.A_ => 30,
			Constants.RegisterTarget.C_ => 32,
			Constants.RegisterTarget.B_ => 34,
			Constants.RegisterTarget.E_ => 36,
			Constants.RegisterTarget.D_ => 38,
			Constants.RegisterTarget.L_ => 40,
			Constants.RegisterTarget.H_ => 42,
			Constants.RegisterTarget.AF_ => 28,
			Constants.RegisterTarget.BC_ => 32,
			Constants.RegisterTarget.DE_ => 36,
			Constants.RegisterTarget.HL_ => 40,
			Constants.RegisterTarget.IX_ => 44,
			Constants.RegisterTarget.IY_ => 48,
			Constants.RegisterTarget.SP_ => 52,
			Constants.RegisterTarget.PC => 56,
			Constants.RegisterTarget.I => 60,
			Constants.RegisterTarget.R => 64,
			_ => throw new ArgumentException($"unknown register {register}", nameof(register))
		};
	}

	public static Constants.RegisterTarget GetAlternateTarget(Constants.RegisterTarget register)
	{
		return register switch
		{
			Constants.RegisterTarget.A => Constants.RegisterTarget.A_,
			Constants.RegisterTarget.F => Constants.RegisterTarget.F_,
			Constants.RegisterTarget.B => Constants.RegisterTarget.B_,
			Constants.RegisterTarget.C => Constants.RegisterTarget.C_,
			Constants.RegisterTarget.D => Constants.RegisterTarget.D_,
			Constants.RegisterTarget.E => Constants.RegisterTarget.E_,
			Constants.RegisterTarget.H => Constants.RegisterTarget.H_,
			Constants.RegisterTarget.L => Constants.RegisterTarget.L_,
			Constants.RegisterTarget.AF => Constants.RegisterTarget.AF_,
			Constants.RegisterTarget.BC => Constants.RegisterTarget.BC_,
			Constants.RegisterTarget.DE => Constants.RegisterTarget.DE_,
			Constants.RegisterTarget.HL => Constants.RegisterTarget.HL_,
			Constants.RegisterTarget.IX => Constants.RegisterTarget.IX_,
			Constants.RegisterTarget.IY => Constants.RegisterTarget.IY_,
			Constants.RegisterTarget.SP => Constants.RegisterTarget.SP_,
			_ => throw new ArgumentException($"register {register} does not have an alternate", nameof(register))
		};
	}
}
