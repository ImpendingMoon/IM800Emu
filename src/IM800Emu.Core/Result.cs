namespace IM800Emu.Core;

/// <summary>
/// For things that can fail, but don't need to participate in the typical exception chain.
/// </summary>
public struct Result
{
	public bool IsSuccess;
	public Exception? Exception;
}

/// <summary>
/// For things that can fail, but don't need to participate in the typical exception chain.
/// Includes a member for returning an object.
/// </summary>
/// <typeparam name="T"></typeparam>
public struct Result<T>
{
	public bool IsSuccess;
	public Exception? Exception;
	public T? ResultObject;
}
