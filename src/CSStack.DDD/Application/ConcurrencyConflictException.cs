namespace CSStack.DDD.Application
{
	/// <summary>
	/// The exception that is thrown when an aggregate could not be persisted because it has been modified by another transaction.
	/// </summary>
	/// <remarks>
	/// The infrastructure throws this exception regardless of how the conflict is detected,
	/// so that the upper layers can handle conflicts with a single type (for example, by returning HTTP 409 or retrying the use case).
	/// </remarks>
	public sealed class ConcurrencyConflictException : Exception
	{
		/// <summary>
		/// Gets the type of the aggregate in which the conflict occurred.
		/// </summary>
		public Type AggregateType { get; }

		/// <summary>
		/// Gets the identifier of the aggregate in which the conflict occurred.
		/// </summary>
		public object AggregateId { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="ConcurrencyConflictException"/> class.
		/// </summary>
		/// <param name="aggregateType">The type of the aggregate in which the conflict occurred.</param>
		/// <param name="aggregateId">The identifier of the aggregate in which the conflict occurred.</param>
		/// <param name="innerException">The exception raised by the data store, if any.</param>
		public ConcurrencyConflictException(Type aggregateType, object aggregateId, Exception? innerException = null)
			: base($"{aggregateType.Name}({aggregateId}) has been modified by another transaction.", innerException)
		{
			AggregateType = aggregateType;
			AggregateId = aggregateId;
		}
	}
}
