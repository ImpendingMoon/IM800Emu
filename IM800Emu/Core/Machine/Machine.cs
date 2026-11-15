using IM800Emu.Core.CPU;
using IM800Emu.Core.Device;
using IM800Emu.Core.IM800Debug;
using Raylib_cs;

namespace IM800Emu.Core.Machine;

public class Machine
{
	private readonly MachineContext _context;
	private readonly VideoDevice _videoDevice;
	private readonly Debugger _debugger;
	private readonly List<ISteppingDevice> _steppingDevices;

	public Machine(byte[] startupRom, List<Symbol> symbols)
	{
		_context = new MachineContext();

		_debugger = new Debugger(_context);

		_steppingDevices = [];

		_context.AddSymbols(symbols);

		// Memory Map:
		// 0x000000-0x1FFFFF: ROM
		// 0x200000-0x3FFFFF: RAM
		// 0x400000-0x5FFFFF: VRAM
		RAMDevice romDevice = new(startupRom, true, "Program ROM", _context);
		RAMDevice ramDevice = new(Config.CpuMemorySizeBytes, "RAM", _context);
		RAMDevice vramDevice = new(4 * 1024, "VRAM", _context);

		_context.MemoryBus.AddDevice(romDevice, Config.MemoryBaseWaitStates, 0x000000, 0x200000);
		_context.MemoryBus.AddDevice(ramDevice, Config.MemoryBaseWaitStates, 0x200000, 0x200000);
		_context.MemoryBus.AddDevice(vramDevice, Config.MemoryBaseWaitStates, 0x400000, 0x200000);

		// IO Map:
		// 0x00-0x03: Video
		// 0x04-0x07: Keyboard
		ControllerDevice controller = new("Controller", _context);
		_videoDevice = new VideoDevice(vramDevice, "VDP", _context);

		_context.IoBus.AddDevice(_videoDevice, Config.IOBaseWaitStates, 0, 4);
		_context.IoBus.AddDevice(controller, Config.IOBaseWaitStates, 4, 4);

		_steppingDevices.Add(_videoDevice);

		_context.Cpu.Reset();
	}

	public Image GetFrame()
	{
		return _videoDevice.GetFrame();
	}

	public void StepFrame()
	{
		if (_context.MachineAction != Constants.MachineAction.Continue)
		{
			_debugger.RunPauseState();
		}

		if (_context.CurrentFrameCyclesRemaining <= 0)
		{
			_context.CurrentFrameCyclesRemaining += _context.CyclesPerFrame;
		}

		while (_context.CurrentFrameCyclesRemaining > 0)
		{
			StepInstruction();

			if (_context.MachineAction != Constants.MachineAction.Continue)
			{
				break;
			}
		}

		_context.Events.RaiseFrameFinished();
	}

	private void StepInstruction()
	{
		uint pc = _context.Cpu.Registers.Read(Constants.RegisterTarget.PC, Constants.DataSize.Dword);
		_context.Events.RaiseBeforeInstructionFetch(pc);
		if (_context.CancelOperation)
		{
			return;
		}

		DecodedOperation decodeResult = _context.Cpu.Decode();
		if (_context.CancelOperation)
		{ return; }
		_context.CurrentOperation = decodeResult;

		int cyclesUsed = decodeResult.FetchCycles;


		cyclesUsed = _context.Cpu.Execute(decodeResult);
		if (_context.CancelOperation)
		{ return; }

		if (cyclesUsed == 0)
		{
			cyclesUsed = 4;
		}

		foreach (var device in _steppingDevices)
		{
			device.Step(cyclesUsed);
		}

		_context.CurrentFrameCyclesRemaining -= cyclesUsed;

		_context.Events.RaiseStepFinished();
	}
}
