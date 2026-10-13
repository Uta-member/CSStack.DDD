namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents a value object, an immutable object whose identity is defined solely by its attributes.
	/// </summary>
	/// <remarks>
	/// Two value objects are equal when all of their attributes are equal.
	/// Validation is intentionally not enforced in the constructor so that persisted values can always be restored;
	/// only the minimum conditions for the type to exist (such as non-null) should be checked there.
	/// Implement <see cref="IValidatable"/> to provide the rules of the value.
	/// </remarks>
	/// <typeparam name="TSelf">The implementing value object type.</typeparam>
	public interface IValueObject<TSelf> : IEquatable<TSelf>
		where TSelf : IValueObject<TSelf>
	{
		/// <summary>
		/// Determines whether two value objects are equal by their attributes.
		/// </summary>
		/// <param name="left">The first value object to compare.</param>
		/// <param name="right">The second value object to compare.</param>
		/// <returns><see langword="true"/> if the value objects are equal; otherwise, <see langword="false"/>.</returns>
		static abstract bool operator ==(TSelf? left, TSelf? right);

		/// <summary>
		/// Determines whether two value objects are not equal by their attributes.
		/// </summary>
		/// <param name="left">The first value object to compare.</param>
		/// <param name="right">The second value object to compare.</param>
		/// <returns><see langword="true"/> if the value objects are not equal; otherwise, <see langword="false"/>.</returns>
		static abstract bool operator !=(TSelf? left, TSelf? right);
	}
}
