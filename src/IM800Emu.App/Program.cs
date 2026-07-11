using System.Diagnostics;
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
			startupRomPath = startupRomPath.Replace("~", userFolder);
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

		Run(machine);
	}

	private static void Run(Machine machine)
	{
		double frameIntervalMs = 1000.0 / Constants.TargetFramerate;
		var stopwatch = new Stopwatch();

		while (true)
		{
			stopwatch.Restart();

			Result result = machine.StepFrame();

			double elapsedMs = stopwatch.ElapsedMilliseconds;
			double sleepMs = frameIntervalMs - elapsedMs;

			if (sleepMs > 0)
			{
				Thread.Sleep((int)sleepMs);
			}
			else
			{
				// Console.WriteLine("Emulator overloaded!");
			}
		}
	}
}
