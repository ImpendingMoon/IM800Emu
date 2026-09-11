using IM800Emu.Core.CPU;
using IM800Emu.Core.IM800Debug;

namespace IM800Emu.Core.Machine;

public class Machine
{
	private readonly MachineContext _context;

	public Machine(byte[] startupRom, List<Symbol> symbols)
	{
		_context = new MachineContext();
		Debugger.AttachDebugger(_context);

		_context.AddSymbols(symbols);

		Result resetResult = _context.Cpu.Reset();

		foreach (Result.Error error in resetResult.Errors)
		{
			Console.WriteLine(error);
		}
	}

	public Result StepFrame()
	{
		Result result = new();

		if (_context.Paused)
		{
			_context.HandlePauseState();
		}

		if (_context.CurrentFrameCyclesRemaining <= 0)
		{
			_context.CurrentFrameCyclesRemaining += _context.CyclesPerFrame;
		}

		while (_context.CurrentFrameCyclesRemaining > 0)
		{
			Result instructionResult = new();

			Result<DecodedOperation> decodeResult = _context.Cpu.Decode();
			instructionResult.Combine(decodeResult);

			_context.CurrentOperation = decodeResult.ResultObject;
			int cyclesUsed = decodeResult.ResultObject.FetchCycles;

			if (decodeResult.IsSuccess)
			{
				Result<int> executeResult = _context.Cpu.Execute(decodeResult.ResultObject);
				instructionResult.Combine(executeResult);
				cyclesUsed = executeResult.ResultObject;
			}

			if (cyclesUsed == 0)
			{
				cyclesUsed = 4; // Typical instruction word fetch + execute timing
			}

			_context.CurrentFrameCyclesRemaining -= cyclesUsed;

			if (!instructionResult.IsSuccess)
			{
				Console.WriteLine();

				foreach (Result.Error error in instructionResult.Errors)
				{
					Console.WriteLine(error);
				}

				string pcString = Debugger.GetNamedAddress(_context, decodeResult.ResultObject.BaseAddress);
				Console.WriteLine(
					$"Instruction: {decodeResult.ResultObject} at {pcString}"
				);
				Console.WriteLine($"Registers: {_context.GetStandardRegisterDisplayString()}");
				_context.Paused = true;
			}

			if (_context.LogExecution)
			{
				Console.WriteLine();
				Console.WriteLine($"Executed: {decodeResult.ResultObject}");

				Result<DecodedOperation> nextOperation = _context.Cpu.Decode();
				if (nextOperation.IsSuccess)
				{
					string pcString = Debugger.GetNamedAddress(_context, decodeResult.ResultObject.BaseAddress);
					Console.WriteLine(
						$"Next Operation: {nextOperation.ResultObject} at {pcString}"
					);
				}
				else
				{
					Console.WriteLine("Not a valid instruction.");
				}

				Console.WriteLine(_context.GetStandardRegisterDisplayString());
				Console.WriteLine();
			}

			if (_context.Paused)
			{
				break;
			}

			result.Combine(instructionResult);
		}

		return result;
	}
}
