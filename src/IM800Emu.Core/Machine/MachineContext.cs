namespace IM800Emu.Core.Machine;

using IM800Emu.Core.Bus;
using IM800Emu.Core.CPU;
using IM800Emu.Core.IM800Debug;

// Used to share context between emulator and debugger
public class MachineContext
{
	private Action<MachineContext, uint, uint>? _handleBreakpointInstruction;
	private Action<MachineContext>? _handlePauseState;
	private Func<MachineContext, string>? _getStandardRegisterDisplayString;
	private Func<MachineContext, string>? _getFullRegisterDisplayString;

	public MachineContext()
	{
		MemoryBus = new();
		IoBus = new();
		InterruptBus = new();
		Cpu = new(MemoryBus, IoBus, InterruptBus, HandleBreakpointInstruction);
		Symbols = [];
		CurrentOperation = new();
	}

	public void AddSymbols(List<Symbol> symbols)
	{
		Symbols.AddRange(symbols);
		Symbols.Sort((a, b) => a.Value.CompareTo(b.Value));
	}

	public void SetBreakpointInstructionHandler(Action<MachineContext, uint, uint> handler)
	{
		_handleBreakpointInstruction = handler;
	}

	public void SetPauseStateHandler(Action<MachineContext> handler)
	{
		_handlePauseState = handler;
	}

	// Used by a debugger to attach symbol names
	public void SetRegisterDisplayStringHandlers(
		Func<MachineContext, string> standardHandler,
		Func<MachineContext, string> fullHandler
	)
	{
		_getStandardRegisterDisplayString = standardHandler;
		_getFullRegisterDisplayString = fullHandler;
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

	public string GetStandardRegisterDisplayString()
	{
		if (_getStandardRegisterDisplayString is not null)
		{
			return _getStandardRegisterDisplayString(this);
		}
		else
		{
			return Cpu.Registers.GetStandardDisplayString();
		}
	}

	public string GetFullRegisterDisplayString()
	{
		if (_getFullRegisterDisplayString is not null)
		{
			return _getFullRegisterDisplayString(this);
		}
		else
		{
			return Cpu.Registers.GetFullDisplayString();
		}
	}

	public IM800 Cpu { get; }
	public MemoryBus MemoryBus { get; }
	public MemoryBus IoBus { get; }
	public InterruptBus InterruptBus { get; }

	public List<Symbol> Symbols { get; }

	public DecodedOperation CurrentOperation { get; set; }

	public readonly int CyclesPerFrame = Constants.CpuSpeedHz / Constants.TargetFramerate;
	public int CurrentFrameCyclesRemaining = 0;
	public bool Paused = false;
	public bool SingleStep = false;
	public bool LogExecution = false;
}