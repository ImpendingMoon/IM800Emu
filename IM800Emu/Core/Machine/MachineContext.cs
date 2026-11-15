using IM800Emu.Core.Bus;
using IM800Emu.Core.CPU;
using IM800Emu.Core.IM800Debug;

namespace IM800Emu.Core.Machine;

// Used to share context between emulator and debugger
public class MachineContext
{
	public readonly int CyclesPerFrame = Config.CpuSpeedHz / Constants.TargetFramerate;
	public int CurrentFrameCyclesRemaining = 0;
	public bool LogExecution = false;

	public MachineContext()
	{
		MemoryBus = new MemoryBus(Config.DataBusWidth, this, "Memory Bus");
		IoBus = new MemoryBus(Config.DataBusWidth, this, "I/O Bus");
		InterruptBus = new InterruptBus();
		Cpu = new IM800(MemoryBus, IoBus, InterruptBus, this);
		Symbols = [];
		CurrentOperation = new DecodedOperation();
		Events = new MachineEvents();
	}

	public IM800 Cpu { get; }
	public MemoryBus MemoryBus { get; }
	public MemoryBus IoBus { get; }
	public InterruptBus InterruptBus { get; }

	public List<Symbol> Symbols { get; }

	public DecodedOperation CurrentOperation { get; set; }
	public MachineEvents Events { get; }
	public Constants.MachineAction MachineAction { get; set; }

	public bool CancelOperation => MachineAction == Constants.MachineAction.Cancel;

	public void AddSymbols(List<Symbol> symbols)
	{
		Symbols.AddRange(symbols);
		Symbols.Sort((a, b) => a.Value.CompareTo(b.Value));
	}
}
