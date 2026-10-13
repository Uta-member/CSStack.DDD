namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Base type for validation errors. The kind of error is represented by the derived type.
	/// </summary>
	/// <remarks>
	/// Derive from this record to define domain-specific errors, and add properties for the values needed to describe the error.
	/// Messages for users are not part of the error; they should be composed by the presentation layer.
	/// </remarks>
	public abstract record ValidationError
	{
		/// <summary>
		/// Gets the location of the error, such as <c>Address.PostalCode</c> or <c>Lines[2].Quantity</c>.
		/// An empty string indicates the object itself.
		/// </summary>
		public string Path { get; init; } = "";
	}

	/// <summary>
	/// Indicates that a value is required but missing.
	/// </summary>
	public sealed record Required : ValidationError;

	/// <summary>
	/// Indicates that a value is not in the expected format.
	/// </summary>
	public sealed record InvalidFormat : ValidationError;

	/// <summary>
	/// Indicates that a value is longer than the maximum length.
	/// </summary>
	/// <param name="MaxLength">The maximum allowed length.</param>
	/// <param name="ActualLength">The actual length of the value.</param>
	public sealed record TooLong(int MaxLength, int ActualLength) : ValidationError;

	/// <summary>
	/// Indicates that a value is shorter than the minimum length.
	/// </summary>
	/// <param name="MinLength">The minimum allowed length.</param>
	/// <param name="ActualLength">The actual length of the value.</param>
	public sealed record TooShort(int MinLength, int ActualLength) : ValidationError;

	/// <summary>
	/// Indicates that a value is outside the allowed range.
	/// </summary>
	/// <typeparam name="T">The type of the value.</typeparam>
	/// <param name="Min">The minimum allowed value.</param>
	/// <param name="Max">The maximum allowed value.</param>
	/// <param name="Actual">The actual value.</param>
	public sealed record OutOfRange<T>(T Min, T Max, T Actual) : ValidationError where T : IComparable<T>;
}
