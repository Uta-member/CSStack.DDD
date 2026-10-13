namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents a value object that wraps a single value, such as an email address, a postal code or a strongly typed identifier.
	/// </summary>
	/// <typeparam name="TSelf">The implementing value object type.</typeparam>
	/// <typeparam name="TValue">The type of the wrapped value.</typeparam>
	public interface ISingleValueObject<TSelf, TValue> : IValueObject<TSelf>
		where TSelf : ISingleValueObject<TSelf, TValue>
		where TValue : notnull
	{
		/// <summary>
		/// Gets the wrapped value.
		/// </summary>
		TValue Value { get; }
	}
}
