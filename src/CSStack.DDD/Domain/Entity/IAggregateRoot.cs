namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents an aggregate root, the entity that forms the consistency boundary of an aggregate.
	/// </summary>
	/// <remarks>
	/// Repositories are defined per aggregate root, and all changes to the aggregate are made through the root.
	/// The aggregate root also records the domain events raised by those changes.
	/// An aggregate root must be a reference type. A struct would be copied on every assignment,
	/// and the events recorded on a copy would be lost.
	/// </remarks>
	/// <typeparam name="TSelf">The implementing aggregate root type.</typeparam>
	/// <typeparam name="TId">The type of the identifier.</typeparam>
	public interface IAggregateRoot<TSelf, TId> : IEntity<TSelf, TId>, IDomainEventSource
		where TSelf : class, IAggregateRoot<TSelf, TId>
		where TId : notnull, IEquatable<TId>;
}
