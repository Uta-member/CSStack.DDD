namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Represents a repository that retrieves and persists an aggregate.
	/// </summary>
	/// <remarks>
	/// The repository does not manage transactions. Changes registered with <see cref="Save"/>
	/// are written when the unit of work in the application layer is committed.
	/// </remarks>
	/// <typeparam name="TAggregate">The type of the aggregate root.</typeparam>
	/// <typeparam name="TId">The type of the identifier of the aggregate root.</typeparam>
	public interface IRepository<TAggregate, TId>
		where TAggregate : IAggregateRoot<TAggregate, TId>
		where TId : notnull, IEquatable<TId>
	{
		/// <summary>
		/// Finds the aggregate with the specified identifier.
		/// </summary>
		/// <param name="id">The identifier of the aggregate.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>The aggregate if found; otherwise, <see langword="null"/>.</returns>
		Task<TAggregate?> FindAsync(TId id, CancellationToken cancellationToken = default);

		/// <summary>
		/// Registers the current state of the aggregate to be persisted.
		/// </summary>
		/// <remarks>
		/// Nothing is written until the unit of work is committed.
		/// Whether the aggregate is inserted or updated is decided by the implementation.
		/// </remarks>
		/// <param name="aggregate">The aggregate to persist.</param>
		void Save(TAggregate aggregate);
	}
}
