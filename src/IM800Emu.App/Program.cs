using System.Diagnostics;
using IM800Emu.Core;
using IM800Emu.Core.IM800Debug;
using IM800Emu.Core.Machine;

namespace IM800Emu.App;

internal class Program
{
	private static void Main(string[] args)
	{
		if (args.Length == 0)
		{
			Console.WriteLine("Usage: IM800Emu <program file> [symbol file]");
			return;
		}

		string startupRomPath = GetFilePath(args[0], mustExist: true);
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

		List<Symbol> symbols = [];
		if (args.Length == 2)
		{
			string symbolFilePath = GetFilePath(args[1], mustExist: true);
			symbols = TryParseSymbolFile(symbolFilePath);
		}

		var machine = new Machine(startupRom, symbols);

		Run(machine);
	}

	private static string GetFilePath(string argument, bool mustExist)
	{
		string filePath = argument.Trim('"');

		if (filePath.StartsWith('~'))
		{
			string userFolder = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			filePath = filePath.Replace("~", userFolder);
		}

		if (mustExist && !File.Exists(filePath))
		{
			Console.WriteLine($"Cannot find the file \"{filePath}\"");
			Environment.Exit(1);
		}

		return filePath;
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

	private static List<Symbol> TryParseSymbolFile(string fileName)
	{
		List<Symbol> result = [];
		string[] lines = File.ReadAllLines(fileName);

		foreach (string line in lines)
		{
			string[] parts = line.Split('|');

			if (parts.Length < 3)
			{
				throw new InvalidOperationException(
					$"Symbol file line must have 3 sections: \"<name>|<type>|<value>\""
				);
			}

			string name = parts[0];
			string typePart = parts[1];
			string valuePart = parts[2];

			Constants.SymbolType type = default;
			long value = default;

			if (!Enum.TryParse(typePart, out type))
			{
				throw new InvalidOperationException($"Unknown symbol type {typePart}");
			}

			// Labels are stored as :X8
			// EQU values are stored as regular ints
			if (type == Constants.SymbolType.Label)
			{
				if (!long.TryParse(
					valuePart,
					System.Globalization.NumberStyles.HexNumber,
					null,
					out value
				))
				{
					throw new InvalidOperationException($"Could not parse value \"{valuePart}\" for label symbol {name}");
				}
			}
			else if (type == Constants.SymbolType.EQU)
			{
				if (!long.TryParse(valuePart, out value))
				{
					throw new InvalidOperationException($"Could not parse value \"{valuePart}\" for EQU symbol {name}");
				}
			}

			Symbol symbol = new(type, name, value);
			result.Add(symbol);
		}

		return result;
	}
}
