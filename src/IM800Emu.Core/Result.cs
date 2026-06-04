namespace IM800Emu.Core;

/// <summary>
/// For actions where failure is an expected result.
/// </summary>
public class Result
{
	private readonly List<string> _errors = [];

	public bool IsSuccess => Errors.Count == 0;
	public IReadOnlyList<string> Errors => _errors;

	/// <summary>
	/// Adds an error string to this result
	/// </summary>
	/// <param name="error"></param>
	public void AddError(string error)
	{
		_errors.Add(error);
	}

	/// <summary>
	/// Appends the errors of the other result to this result
	/// </summary>
	/// <param name="other"></param>
	public void Combine(Result other)
	{
		_errors.AddRange(other._errors);
	}
}

/// <summary>
/// For actions where failure is an expected result.
/// Includes a member for returning an object.
/// </summary>
/// <typeparam name="T">Type of the result object</typeparam>
public class Result<T> : Result
{
	public Result(T result)
	{
		ResultObject = result;
	}

	public T ResultObject { get; set; }
}
