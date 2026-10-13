namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Base class for aggregate roots that records domain events raised by the aggregate.
	/// </summary>
	/// <typeparam name="TSelf">The derived aggregate root type.</typeparam>
	/// <typeparam name="TId">The type of the identifier.</typeparam>
	public abstract class AggregateRoot<TSelf, TId> : Entity<TSelf, TId>, IAggregateRoot<TSelf, TId>
		where TSelf : AggregateRoot<TSelf, TId>
		where TId : notnull, IEquatable<TId>
	{
		private readonly List<IDomainEvent> _domainEvents = [];

		/// <inheritdoc/>
		public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

		/// <summary>
		/// Records a domain event raised by an operation of this aggregate.
		/// </summary>
		/// <remarks>
		/// Do not raise events when the aggregate is reconstructed from persistence,
		/// because reconstruction restores past state and nothing new has happened.
		/// </remarks>
		/// <param name="domainEvent">The domain event to record.</param>
		protected void AddDomainEvent(IDomainEvent domainEvent)
		{
			_domainEvents.Add(domainEvent);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// This member is implemented explicitly so that it can be called only through <see cref="IDomainEventSource"/>.
		/// Pulling the events clears them, and if application services or domain code pulled them,
		/// the events would not be written to the outbox when changes are committed.
		/// </remarks>
		IReadOnlyList<IDomainEvent> IDomainEventSource.PullDomainEvents()
		{
			IReadOnlyList<IDomainEvent> events = [.. _domainEvents];
			_domainEvents.Clear();
			return events;
		}
	}
}
