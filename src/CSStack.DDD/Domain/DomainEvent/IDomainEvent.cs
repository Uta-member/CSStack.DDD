namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents a domain event, a fact that something meaningful to the business has happened.
	/// </summary>
	/// <remarks>
	/// Name domain events in the past tense (for example, <c>MemberRegistered</c>) and implement them as immutable records.
	/// Copy the values that consumers need instead of referencing the aggregate itself.
	/// </remarks>
	public interface IDomainEvent
	{
		/// <summary>
		/// Gets the unique identifier of the event, which consumers can use to detect duplicate deliveries.
		/// </summary>
		Guid EventId { get; }

		/// <summary>
		/// Gets the date and time when the event occurred.
		/// </summary>
		DateTimeOffset OccurredAt { get; }
	}
}
