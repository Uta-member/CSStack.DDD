namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents a repository for an aggregate that can be deleted.
	/// </summary>
	/// <remarks>
	/// Use this interface only for aggregates that actually cease to exist in the domain.
	/// If deletion has a business meaning (for example, withdrawal or cancellation),
	/// model it as a state transition of the aggregate and persist it with <see cref="IRepository{TAggregate, TId}.Save"/> instead.
	/// </remarks>
	/// <typeparam name="TAggregate">The type of the aggregate root.</typeparam>
	/// <typeparam name="TId">The type of the identifier of the aggregate root.</typeparam>
	public interface IDeletableRepository<TAggregate, TId> : IRepository<TAggregate, TId>
		where TAggregate : IAggregateRoot<TAggregate, TId>
		where TId : notnull, IEquatable<TId>
	{
		/// <summary>
		/// Registers the aggregate to be deleted.
		/// </summary>
		/// <remarks>
		/// Nothing is deleted until the unit of work is committed.
		/// Whether the data is physically deleted or kept for auditing is decided by the implementation.
		/// </remarks>
		/// <param name="aggregate">The aggregate to delete.</param>
		void Delete(TAggregate aggregate);
	}
}
