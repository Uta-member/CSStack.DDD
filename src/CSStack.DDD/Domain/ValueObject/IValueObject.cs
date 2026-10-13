namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents a value object, an immutable object whose identity is defined solely by its attributes.
	/// </summary>
	/// <remarks>
	/// Two value objects are equal when all of their attributes are equal.
	/// Implementing it as a <see langword="record"/> is recommended, because the compiler generates
	/// <see cref="object.Equals(object)"/>, <see cref="object.GetHashCode"/> and the <c>==</c> and <c>!=</c> operators.
	/// Validation is intentionally not enforced in the constructor so that persisted values can always be restored;
	/// only the minimum conditions for the type to exist (such as non-null) should be checked there.
	/// Implement <see cref="IValidatable"/> to provide the rules of the value.
	/// </remarks>
	/// <typeparam name="TSelf">The implementing value object type.</typeparam>
	public interface IValueObject<TSelf> : IEquatable<TSelf>
		where TSelf : IValueObject<TSelf>;
}
