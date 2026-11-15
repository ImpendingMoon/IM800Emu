namespace IM800Emu.Core.Helpers;

public static class SizeHelper
{
	public static int GetDataSizeBytes(Constants.DataSize size)
	{
		return size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.Dword => 4,
			_ => throw new ArgumentException($"invalid size {size}", nameof(size))
		};
	}
}
