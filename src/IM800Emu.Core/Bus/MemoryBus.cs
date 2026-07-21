using System.Buffers.Binary;
using System.Numerics;
using IM800Emu.Core.Device;

namespace IM800Emu.Core.Bus;

/// <summary>
/// </summary>
public class MemoryBus
{
	private readonly List<DeviceMapping> _mappings = [];

	public void AddDevice(IMemoryDevice device, int waitStates, uint baseAddress, uint addressSpaceLength)
	{
		uint roundedLength = BitOperations.RoundUpToPowerOf2(addressSpaceLength);

		var newMapping = new DeviceMapping(device, waitStates, baseAddress, addressSpaceLength);

		// Fail if overlapping
		foreach (DeviceMapping mapping in _mappings)
		{
			if (baseAddress >= mapping.BaseAddress && newMapping.MaxAddress < mapping.MaxAddress)
			{
				throw new InvalidOperationException("new device overlaps with existing device's address space");
			}
		}

		_mappings.Add(newMapping);
	}

	public Result<MemoryOperation> Read(uint address, Constants.DataSize size)
	{
		MemoryOperation resultObject = new();
		Result<MemoryOperation> result = new(resultObject);

		int bytesToRead = size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.Dword => 4,
			_ => throw new ArgumentException($"invalid size {size}", nameof(size))
		};

		byte[] bytes = new byte[4];

		// Read data, accumulate cycles
		for (int i = 0; i < bytesToRead; i++)
		{
			uint currentAddress = address + (uint)i;
			Result<MemoryOperation> currentRead = ReadByteInternal(currentAddress);
			result.Combine(currentRead);

			result.ResultObject.Cycles += currentRead.ResultObject.Cycles;
			bytes[i] = (byte)currentRead.ResultObject.Data;
		}

		result.ResultObject.Data = BinaryPrimitives.ReadUInt32LittleEndian(bytes);

		return result;
	}

	public Result<MemoryOperation> Write(uint address, Constants.DataSize size, uint data)
	{
		MemoryOperation resultObject = new() { Data = data };
		Result<MemoryOperation> result = new(resultObject);

		byte[] bytes = new byte[4];
		BinaryPrimitives.WriteUInt32LittleEndian(bytes, data);

		int bytesToWrite = size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.Dword => 4,
			_ => throw new ArgumentException($"invalid size {size}", nameof(size))
		};

		// Write data, accumulate cycles
		for (int i = 0; i < bytesToWrite; i++)
		{
			uint currentAddress = address + (uint)i;
			byte value = bytes[i];

			Result<MemoryOperation> currentWrite = WriteByteInternal(currentAddress, value);
			result.Combine(currentWrite);

			result.ResultObject.Cycles += currentWrite.ResultObject.Cycles;
		}

		return result;
	}

	private Result<MemoryOperation> ReadByteInternal(uint address)
	{
		MemoryOperation resultObject = new() { Cycles = Constants.MemoryBaseWaitStates };

		Result<MemoryOperation> result = new(resultObject);

		Result<DeviceMapping?> findResult = FindDeviceMapping(address);
		result.Combine(findResult);

		Result<byte?> readResult = new(null);

		if (findResult.ResultObject is not null)
		{
			uint effectiveAddress = address - findResult.ResultObject.BaseAddress;
			readResult = findResult.ResultObject.Device.Read(effectiveAddress);
			result.Combine(readResult);
			resultObject.Cycles = findResult.ResultObject.WaitStates;
		}

		// Value or open bus
		result.ResultObject.Data = readResult.ResultObject ?? 0xFF;

		return result;
	}

	private Result<MemoryOperation> WriteByteInternal(uint address, byte value)
	{
		MemoryOperation resultObject = new() { Cycles = Constants.MemoryBaseWaitStates };
		Result<MemoryOperation> result = new(resultObject);

		Result<DeviceMapping?> findResult = FindDeviceMapping(address);
		result.Combine(findResult);

		if (findResult.ResultObject is not null)
		{
			uint effectiveAddress = address - findResult.ResultObject.BaseAddress;
			Result writeResult = findResult.ResultObject.Device.Write(effectiveAddress, value);
			result.Combine(writeResult);
			resultObject.Cycles = findResult.ResultObject.WaitStates;
		}

		return result;
	}

	private Result<DeviceMapping?> FindDeviceMapping(uint address)
	{
		Result<DeviceMapping?> result = new(null);

		foreach (DeviceMapping mapping in _mappings)
		{
			if (address >= mapping.BaseAddress && address <= mapping.MaxAddress)
			{
				result.ResultObject = mapping;
				break;
			}
		}

		if (result.ResultObject is null)
		{
			result.AddError(nameof(MemoryBus), $"no device mapped at address 0x{address:X}");
		}

		return result;
	}

	private class DeviceMapping
	{
		public DeviceMapping(IMemoryDevice device, int waitStates, uint baseAddress, uint addressSpaceLength)
		{
			WaitStates =  waitStates;
			Device = device;
			BaseAddress = baseAddress;
			AddressSpaceLength = addressSpaceLength;
		}

		public IMemoryDevice Device { get; }
		public int WaitStates { get; }
		public uint BaseAddress { get; }
		public uint AddressSpaceLength { get; }
		public uint MaxAddress => BaseAddress + AddressSpaceLength;
	}
}
