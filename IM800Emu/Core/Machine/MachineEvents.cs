using IM800Emu.Core.CPU;

namespace IM800Emu.Core.Machine;

public class MachineEvents
{
	public event EventHandler? BeforeMemoryRead;
	public event EventHandler? BeforeMemoryWrite;
	public event EventHandler? BeforeBadMemoryRead;
	public event EventHandler? BeforeBadMemoryWrite;

	public event EventHandler? BeforeInstructionFetch;
	public event EventHandler? InvalidInstruction;
	public event EventHandler? BkptInstruction;

	public event EventHandler? StepFinished;
	public event EventHandler? FrameFinished;

	internal void RaiseBeforeMemoryRead(uint address, Constants.DataSize dataSize, string deviceName)
	{
		var args = new BeforeMemoryReadArgs()
		{
			Address = address,
			DataSize = dataSize,
			DeviceName = deviceName,
		};
		BeforeMemoryRead?.Invoke(this, args);
	}

	internal void RaiseBeforeMemoryWrite(uint address, Constants.DataSize dataSize, uint value, string deviceName)
	{
		var args = new BeforeMemoryWriteArgs()
		{
			Address = address,
			DataSize = dataSize,
			Value = value,
			DeviceName = deviceName,
		};
		BeforeMemoryWrite?.Invoke(this, args);
	}

	internal void RaiseBeforeBadMemoryRead(uint address, Constants.DataSize dataSize, string deviceName)
	{
		var args = new BeforeBadMemoryReadArgs()
		{
			Address = address,
			DataSize = dataSize,
			DeviceName = deviceName,
		};
		BeforeMemoryRead?.Invoke(this, args);
	}

	internal void RaiseBeforeBadMemoryWrite(uint address, Constants.DataSize dataSize, uint value, string deviceName)
	{
		var args = new BeforeBadMemoryWriteArgs()
		{
			Address = address,
			DataSize = dataSize,
			Value = value,
			DeviceName = deviceName,
		};
		BeforeMemoryWrite?.Invoke(this, args);
	}

	internal void RaiseBeforeInstructionFetch(uint programCounter)
	{
		var args = new BeforeInstructionFetchArgs()
		{
			ProgramCounter = programCounter,
		};
		BeforeInstructionFetch?.Invoke(this, args);
	}
	internal void RaiseInvalidInstruction(DecodedOperation operation, string message)
	{
		var args = new InvalidInstructionArgs()
		{
			DecodedOperation = operation,
			Message = message,
		};
		InvalidInstruction?.Invoke(this, args);
	}

	internal void RaiseBkptInstruction(DecodedOperation operation)
	{
		var args = new BkptInstructionArgs()
		{
			DecodedOperation = operation,
		};
		BkptInstruction?.Invoke(this, args);
	}

	internal void RaiseStepFinished()
	{
		var args = new StepFinishedArgs();
		StepFinished?.Invoke(this, args);
	}
	internal void RaiseFrameFinished()
	{
		var args = new FrameFinishedArgs();
		FrameFinished?.Invoke(this, args);
	}

	public class BeforeMemoryReadArgs : EventArgs
	{
		public uint Address { get; set; }
		public Constants.DataSize DataSize { get; set; }
		public required string DeviceName { get; set; }
	}

	public class BeforeMemoryWriteArgs : EventArgs
	{
		public uint Address { get; set; }
		public Constants.DataSize DataSize { get; set; }
		public uint Value { get; set; }
		public required string DeviceName { get; set; }
	}

	public class BeforeBadMemoryReadArgs : EventArgs
	{
		public uint Address { get; set; }
		public Constants.DataSize DataSize { get; set; }
		public required string DeviceName { get; set; }
	}

	public class BeforeBadMemoryWriteArgs : EventArgs
	{
		public uint Address { get; set; }
		public Constants.DataSize DataSize { get; set; }
		public uint Value { get; set; }
		public required string DeviceName { get; set; }
	}

	public class BeforeInstructionFetchArgs : EventArgs
	{
		public uint ProgramCounter { get; set; }
	}

	public class InvalidInstructionArgs : EventArgs
	{
		public required DecodedOperation DecodedOperation { get; set; }
		public required string Message { get; set; }
	}

	public class BkptInstructionArgs : EventArgs
	{
		public required DecodedOperation DecodedOperation { get; set; }
	}

	public class StepFinishedArgs : EventArgs
	{

	}

	public class FrameFinishedArgs : EventArgs
	{

	}
}
