using IM800Emu.Core.Device;

namespace IM800Emu.Core.Bus;

/// <summary>
/// Provides access to the memory bus and maps memory devices into the processor's address space.
/// Note: This cannot access multiple devices on a single access.
/// </summary>
public class MemoryBus
{
	private readonly List<DeviceMapping> _mappings = [];

	public MemoryBus(Constants.DataSize busWidth)
	{
		if (!IsValidDataSize(busWidth))
		{
			throw new ArgumentException($"invalid bus width {busWidth}", nameof(busWidth));
		}

		BusWidth = busWidth;
	}

	/// <summary>
	/// The maximum transfer size supported by the bus.
	/// </summary>
	public Constants.DataSize BusWidth { get; }

	public void AddDevice(IMemoryDevice device, int waitStates, uint baseAddress, uint addressSpaceLength)
	{
		if (addressSpaceLength == 0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(addressSpaceLength),
				"address space length must be greater than zero"
			);
		}

		uint newMaxAddress = baseAddress + addressSpaceLength - 1;

		// Detect any overlap between the new mapping and an existing mapping.
		foreach (DeviceMapping mapping in _mappings)
		{
			if (baseAddress <= mapping.MaxAddress && newMaxAddress >= mapping.BaseAddress)
			{
				throw new InvalidOperationException("new device overlaps with existing device's address space");
			}
		}

		DeviceMapping newMapping = new(device, waitStates, baseAddress, addressSpaceLength);
		_mappings.Add(newMapping);
	}

	public Result<MemoryOperation> Read(uint address, Constants.DataSize size)
	{
		int bytesToRead = GetSizeInBytes(size);

		MemoryOperation resultObject = new();
		Result<MemoryOperation> result = new(resultObject);

		uint data = 0;
		int offset = 0;

		while (offset < bytesToRead)
		{
			uint currentAddress = address + (uint)offset;
			int remaining = bytesToRead - offset;

			int transferSize = GetTransferSize(currentAddress, remaining);

			Constants.DataSize transferDataSize = ToDataSize(transferSize);

			Result<MemoryOperation> transfer = ReadBusTransaction(currentAddress, transferDataSize);

			result.Combine(transfer);
			resultObject.Cycles += transfer.ResultObject.Cycles;

			data |= transfer.ResultObject.Data << (offset * 8);

			offset += transferSize;
		}

		resultObject.Data = data;

		return result;
	}

	public Result<MemoryOperation> Write(uint address, Constants.DataSize size, uint data)
	{
		int bytesToWrite = GetSizeInBytes(size);

		MemoryOperation resultObject = new()
		{
			Data = data
		};

		Result<MemoryOperation> result = new(resultObject);

		int offset = 0;

		while (offset < bytesToWrite)
		{
			uint currentAddress = address + (uint)offset;
			int remaining = bytesToWrite - offset;

			int transferSize = GetTransferSize(currentAddress, remaining);

			Constants.DataSize transferDataSize = ToDataSize(transferSize);

			uint transferData = ExtractBytes(data, offset, transferSize);

			Result<MemoryOperation> transfer = WriteBusTransaction(currentAddress, transferDataSize, transferData);

			result.Combine(transfer);
			resultObject.Cycles += transfer.ResultObject.Cycles;

			offset += transferSize;
		}

		return result;
	}

	/// <summary>
	/// Calculates the largest bus transaction that can be performed based on alignment and bytes needed
	/// </summary>
	private int GetTransferSize(uint address, int remaining)
	{
		int busBytes = GetSizeInBytes(BusWidth);

		// Prefer the largest possible naturally aligned transaction.
		if (busBytes >= 4 && remaining >= 4 && address % 4 == 0)
		{
			return 4;
		}

		if (busBytes >= 2 && remaining >= 2 && address % 2 == 0)
		{
			return 2;
		}

		return 1;
	}

	private Result<MemoryOperation> ReadBusTransaction(uint address, Constants.DataSize size)
	{
		MemoryOperation resultObject = new()
		{
			Cycles = Constants.MemoryBaseWaitStates
		};

		Result<MemoryOperation> result = new(resultObject);

		Result<DeviceMapping?> findResult = FindDeviceMapping(address);

		result.Combine(findResult);

		if (findResult.ResultObject is null)
		{
			// Open bus. Return all ones.
			resultObject.Data = size switch
			{
				Constants.DataSize.Byte => 0xFF,
				Constants.DataSize.Word => 0xFFFF,
				Constants.DataSize.Dword => 0xFFFFFFFF,
				_ => throw new ArgumentException($"invalid transaction size {size}", nameof(size))
			};

			return result;
		}

		DeviceMapping mapping = findResult.ResultObject;

		uint effectiveAddress = address - mapping.BaseAddress;

		Result<uint?> readResult = mapping.Device.Read(effectiveAddress, size);

		result.Combine(readResult);

		resultObject.Data = readResult.ResultObject ?? 0xFFFFFFFF;

		resultObject.Cycles = mapping.WaitStates;

		return result;
	}

	private Result<MemoryOperation> WriteBusTransaction(uint address, Constants.DataSize size, uint data)
	{
		MemoryOperation resultObject = new()
		{
			Cycles = Constants.MemoryBaseWaitStates
		};

		Result<MemoryOperation> result = new(resultObject);

		Result<DeviceMapping?> findResult = FindDeviceMapping(address);

		result.Combine(findResult);

		if (findResult.ResultObject is null)
		{
			return result;
		}

		DeviceMapping mapping = findResult.ResultObject;

		uint effectiveAddress = address - mapping.BaseAddress;

		Result writeResult = mapping.Device.Write(effectiveAddress, size, data);

		result.Combine(writeResult);

		resultObject.Cycles = mapping.WaitStates;

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

	private static int GetSizeInBytes(Constants.DataSize size)
	{
		return size switch
		{
			Constants.DataSize.Byte => 1,
			Constants.DataSize.Word => 2,
			Constants.DataSize.Dword => 4,
			_ => throw new ArgumentException($"invalid size {size}", nameof(size))
		};
	}

	private static Constants.DataSize ToDataSize(int bytes)
	{
		return bytes switch
		{
			1 => Constants.DataSize.Byte,
			2 => Constants.DataSize.Word,
			4 => Constants.DataSize.Dword,
			_ => throw new ArgumentOutOfRangeException(nameof(bytes))
		};
	}

	private static uint ExtractBytes(uint data, int offset, int size)
	{
		return size switch
		{
			1 => (data >> (offset * 8)) & 0xFF,
			2 => (data >> (offset * 8)) & 0xFFFF,
			4 => data,
			_ => throw new ArgumentOutOfRangeException(nameof(size))
		};
	}

	private static bool IsValidDataSize(Constants.DataSize size)
	{
		return size is Constants.DataSize.Byte or Constants.DataSize.Word or Constants.DataSize.Dword;
	}

	private class DeviceMapping
	{
		public DeviceMapping(IMemoryDevice device, int waitStates, uint baseAddress, uint addressSpaceLength)
		{
			WaitStates = waitStates;
			Device = device;
			BaseAddress = baseAddress;
			AddressSpaceLength = addressSpaceLength;
		}

		public IMemoryDevice Device { get; }
		public int WaitStates { get; }
		public uint BaseAddress { get; }
		public uint AddressSpaceLength { get; }
		public uint MaxAddress => BaseAddress + AddressSpaceLength - 1;
	}
}
