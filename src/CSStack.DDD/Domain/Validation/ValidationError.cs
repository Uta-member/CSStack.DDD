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
	/// <remarks>
	/// Use this for the length of a string or a collection. For a numeric value, use <see cref="AboveMaximum"/>.
	/// </remarks>
	/// <param name="MaxLength">The maximum allowed length.</param>
	/// <param name="ActualLength">The actual length of the value.</param>
	public sealed record TooLong(int MaxLength, int ActualLength) : ValidationError;

	/// <summary>
	/// Indicates that a value is shorter than the minimum length.
	/// </summary>
	/// <remarks>
	/// Use this for the length of a string or a collection. For a numeric value, use <see cref="BelowMinimum"/>.
	/// </remarks>
	/// <param name="MinLength">The minimum allowed length.</param>
	/// <param name="ActualLength">The actual length of the value.</param>
	public sealed record TooShort(int MinLength, int ActualLength) : ValidationError;

	/// <summary>
	/// Indicates that a value is less than the minimum value.
	/// </summary>
	/// <remarks>
	/// Use this for numeric values such as quantities and amounts. For the length of a string or a collection, use <see cref="TooShort"/>.
	/// The type is not generic so that it can be matched with <c>case BelowMinimum</c> and serialized with a single type discriminator.
	/// <see cref="decimal"/> represents integers and decimal numbers exactly.
	/// Define your own error for other kinds of values, such as dates, or for an exclusive bound.
	/// A range is checked as two bounds, so a rule with only one bound can also be represented.
	/// </remarks>
	/// <param name="Minimum">The minimum allowed value, inclusive.</param>
	/// <param name="Actual">The actual value.</param>
	public sealed record BelowMinimum(decimal Minimum, decimal Actual) : ValidationError;

	/// <summary>
	/// Indicates that a value is greater than the maximum value.
	/// </summary>
	/// <remarks>
	/// Use this for numeric values such as quantities and amounts. For the length of a string or a collection, use <see cref="TooLong"/>.
	/// See <see cref="BelowMinimum"/> for the design.
	/// </remarks>
	/// <param name="Maximum">The maximum allowed value, inclusive.</param>
	/// <param name="Actual">The actual value.</param>
	public sealed record AboveMaximum(decimal Maximum, decimal Actual) : ValidationError;
}
