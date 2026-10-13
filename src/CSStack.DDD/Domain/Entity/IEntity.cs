namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents an entity, an object defined by its identity rather than its attributes.
	/// </summary>
	/// <remarks>
	/// Two entities are equal when they are of the same type and have the same identifier,
	/// regardless of the values of their other attributes.
	/// Derive from <see cref="Entity{TSelf, TId}"/> to get this equality, including the <c>==</c> and <c>!=</c> operators.
	/// </remarks>
	/// <typeparam name="TSelf">The implementing entity type.</typeparam>
	/// <typeparam name="TId">The type of the identifier.</typeparam>
	public interface IEntity<TSelf, TId> : IEquatable<TSelf>
		where TSelf : IEntity<TSelf, TId>
		where TId : notnull, IEquatable<TId>
	{
		/// <summary>
		/// Gets the identifier of the entity. The identifier never changes during the lifetime of the entity.
		/// </summary>
		TId Id { get; }
	}
}
