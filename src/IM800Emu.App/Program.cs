using IM800Emu.Core;
using IM800Emu.Core.Machine;

namespace IM800Emu.App;

internal class Program
{
	private static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine("Usage: IM800Emu <program file>");
			return;
		}

		string startupRomPath = args[0];
		startupRomPath = startupRomPath.Trim('"');

		if (startupRomPath.StartsWith('~'))
		{
			string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			startupRomPath.Replace("~", userFolder);
		}

		if (!File.Exists(startupRomPath))
		{
			Console.WriteLine($"Cannot find the file \"{startupRomPath}\"");
			return;
		}

		byte[] startupRom = [];

		try
		{
			startupRom = File.ReadAllBytes(startupRomPath);
		}
		catch (Exception ex)
		{
			Console.WriteLine($"Could not read \"{startupRomPath}\": {ex.Message}");
			return;
		}

		var machine = new Machine(startupRom);

		while (true)
		{
			Result result = machine.StepFrame();
			if (!result.IsSuccess)
			{
				foreach (Result.Error error in result.Errors)
				{
					Console.WriteLine(error);
				}
				return;
			}
		}
	}
}
