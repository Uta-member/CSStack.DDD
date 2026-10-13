namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents an entity, an object defined by its identity rather than its attributes.
	/// </summary>
	/// <remarks>
	/// Two entities are equal when they are of the same type and have the same identifier,
	/// regardless of the values of their other attributes.
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

		/// <summary>
		/// Determines whether two entities are equal by their identifiers.
		/// </summary>
		/// <param name="left">The first entity to compare.</param>
		/// <param name="right">The second entity to compare.</param>
		/// <returns><see langword="true"/> if the entities have the same identifier; otherwise, <see langword="false"/>.</returns>
		static abstract bool operator ==(TSelf? left, TSelf? right);

		/// <summary>
		/// Determines whether two entities are not equal by their identifiers.
		/// </summary>
		/// <param name="left">The first entity to compare.</param>
		/// <param name="right">The second entity to compare.</param>
		/// <returns><see langword="true"/> if the entities have different identifiers; otherwise, <see langword="false"/>.</returns>
		static abstract bool operator !=(TSelf? left, TSelf? right);
	}
}
