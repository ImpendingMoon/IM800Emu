namespace IM800Emu.Core;

/// <summary>
/// For actions where failure is an expected result, and the usual try/catch is slow or harder to read.
/// </summary>
public class Result
{
	public bool IsSuccess => Exceptions.Count == 0;
	public List<Exception> Exceptions { get; set; } = [];
}

/// <summary>
/// For actions where failure is an expected result, and the usual try/catch is slow or harder to read.
/// Includes a member for returning an object.
/// This object is intentionally allowed to have a value when not successful, in the case where an operation may
/// be partially successful and/or may need to continue from a previous state.
/// </summary>
/// <typeparam name="T">Type of the result object</typeparam>
public class Result<T> : Result
{
	public T? ResultObject { get; set; }
}
