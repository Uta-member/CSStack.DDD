namespace CSStack.DDD.Application
{
	/// <summary>
	/// The exception that is thrown when the aggregate requested by a use case does not exist.
	/// </summary>
	/// <remarks>
	/// Throw this exception from the application layer, typically through
	/// <see cref="RepositoryExtensions.GetAsync{TAggregate, TId}(Domain.IRepository{TAggregate, TId}, TId, CancellationToken)"/>,
	/// so that the upper layers can handle missing aggregates with a single type (for example, by returning HTTP 404).
	/// It does not derive from <see cref="Domain.DomainException"/>, because a missing aggregate is not an operation rejected by a rule of the domain.
	/// </remarks>
	public sealed class AggregateNotFoundException : Exception
	{
		/// <summary>
		/// Gets the type of the aggregate that was not found.
		/// </summary>
		public Type AggregateType { get; }

		/// <summary>
		/// Gets the identifier of the aggregate that was not found.
		/// </summary>
		public object AggregateId { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="AggregateNotFoundException"/> class.
		/// </summary>
		/// <param name="aggregateType">The type of the aggregate that was not found.</param>
		/// <param name="aggregateId">The identifier of the aggregate that was not found.</param>
		public AggregateNotFoundException(Type aggregateType, object aggregateId)
			: base($"{aggregateType.Name}({aggregateId}) was not found.")
		{
			AggregateType = aggregateType;
			AggregateId = aggregateId;
		}
	}
}
