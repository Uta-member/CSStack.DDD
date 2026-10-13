namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Base type for immutable domain events with an automatically generated identifier.
	/// </summary>
	/// <remarks>
	/// Receive the time of occurrence from the caller instead of reading the clock in the domain.
	/// When restoring an event from persistence, initialize EventId with the stored identifier
	/// so that consumers can still detect duplicate deliveries. Record copies preserve the identifier.
	/// </remarks>
	/// <param name="OccurredAt">The date and time when the event occurred.</param>
	public abstract record DomainEvent(DateTimeOffset OccurredAt) : IDomainEvent
	{
		/// <summary>
		/// Gets the unique identifier of the event. Defaults to a newly generated <see cref="Guid"/>.
		/// </summary>
		public Guid EventId { get; init; } = Guid.NewGuid();
	}
}
