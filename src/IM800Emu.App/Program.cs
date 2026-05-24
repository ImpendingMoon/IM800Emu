namespace IM800Emu.App;

internal class Program
{
	private static void Main(string[] args)
	{
		var machine = new Core.Machine.Machine([]);
		machine.StepFrame();
	}
}
