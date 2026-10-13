namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents an object that records domain events.
	/// </summary>
	/// <remarks>
	/// This non-generic interface allows the infrastructure (for example, a unit of work) to collect domain events
	/// from aggregates of any type without reflection.
	/// </remarks>
	public interface IDomainEventSource
	{
		/// <summary>
		/// Gets the domain events recorded since they were last pulled.
		/// </summary>
		IReadOnlyList<IDomainEvent> DomainEvents { get; }

		/// <summary>
		/// Returns the recorded domain events and clears them.
		/// </summary>
		/// <remarks>
		/// Retrieval and clearing are combined into a single operation so that events are not dispatched twice.
		/// This is intended to be called by the infrastructure when changes are committed.
		/// </remarks>
		/// <returns>The domain events recorded since they were last pulled.</returns>
		IReadOnlyList<IDomainEvent> PullDomainEvents();
	}
}
