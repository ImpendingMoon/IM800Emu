namespace IM800Emu.Core.CPU;

public partial class IM800
{
	public Result<int> ExecuteInvalid(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteHalted(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteInterrupted(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteLD(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteEX(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecutePUSH(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecutePOP(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteEXH(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteLEA(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteEXA(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteEXX(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteEXI(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteIN_OUT(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteADD(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteADC(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteSUB(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteSBC(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteCP(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteINC(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteDEC(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteNEG(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteEXT(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteMLT(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteDIV(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteSDIV(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteDAA(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteAND(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteOR(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteXOR(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteTST(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteCPL(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteBIT(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteSET(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRES(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRLC(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRRC(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRL(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRR(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteSLA(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteSRA(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteSRL(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRLD(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRRD(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteNOP(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteJP(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteJR(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteDJNZ(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteJAZ(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteJANZ(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteCALL(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteCR(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRET(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRETI(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRETN(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteRST(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteSCF(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteCCF(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteEI(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteDI(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteIM1(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteIM2(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteHALT(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteLDI(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteLDAR(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteLDRA(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteBLD(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteBCP(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteBTST(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteBIN(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}

	public Result<int> ExecuteBOUT(DecodedOperation operation)
	{
		Result<int> result = new(4);
		result.AddError(_executeErrorName, $"Execute{operation.Operation} is not implemented");
		return result;
	}
}
