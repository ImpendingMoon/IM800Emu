namespace IM800Emu.Core;

/// <summary>
///     For actions where failure is an expected result.
/// </summary>
public class Result
{
	private readonly List<Error> _errors = [];

	public bool IsSuccess => Errors.Count == 0;
	public IReadOnlyList<Error> Errors => _errors;

	/// <summary>
	///     Adds an error string to this result
	/// </summary>
	/// <param name="message"></param>
	public void AddError(string source, string message)
	{
		Error error = new(source, message);
		_errors.Add(error);
	}

	/// <summary>
	///     Appends the errors of the other result to this result
	/// </summary>
	/// <param name="other"></param>
	public void Combine(Result other)
	{
		_errors.AddRange(other._errors);
	}

	public class Error
	{
		public Error(string source, string message)
		{
			Source = source;
			Message = message;
		}

		public string Source { get; set; }
		public string Message { get; set; }

		public override string ToString()
		{
			return $"{Source}: {Message}";
		}
	}
}

/// <summary>
///     For actions where failure is an expected result.
///     Includes a member for returning an object.
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
