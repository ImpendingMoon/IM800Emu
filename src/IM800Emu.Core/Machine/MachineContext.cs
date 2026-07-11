namespace IM800Emu.Core.Machine;

using IM800Emu.Core.Bus;
using IM800Emu.Core.CPU;

// Used to share context between emulator and debugger
public class MachineContext
{
	public MachineContext()
	{
		MemoryBus = new();
		IoBus = new();
		InterruptBus = new();
		Cpu = new(MemoryBus, IoBus, InterruptBus, HandleBreakpointInstruction);
	}

	public void SetBreakpointInstructionHandler(Action<MachineContext, uint, uint> handler)
	{
		_handleBreakpointInstruction = handler;
	}

	public void SetPauseStateHandler(Action<MachineContext> handler)
	{
		_handlePauseState = handler;
	}

	public void HandlePauseState()
	{
		if (_handlePauseState is not null)
		{
			_handlePauseState(this);
		}
	}

	public void HandleBreakpointInstruction(uint baseAddress, uint code)
	{
		if (_handleBreakpointInstruction is not null)
		{
			_handleBreakpointInstruction(this, baseAddress, code);
		}
	}

	public IM800 Cpu { get; }
	public MemoryBus MemoryBus { get; }
	public MemoryBus IoBus { get; }
	public InterruptBus InterruptBus { get; }

	public readonly int CyclesPerFrame = Constants.CpuSpeedHz / Constants.TargetFramerate;
	public int CurrentFrameCyclesRemaining = 0;
	public bool Paused = false;
	public bool SingleStep = false;
	public bool LogExecution = false;

	private Action<MachineContext, uint, uint>? _handleBreakpointInstruction;
	private Action<MachineContext>? _handlePauseState;
}