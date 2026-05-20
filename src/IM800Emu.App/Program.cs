namespace IM800Emu.App;

internal class Program
{
	static void Main(string[] args)
	{
		var machine = new Core.Machine.Machine([]);
		machine.StepFrame();
	}
}