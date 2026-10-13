namespace CSStack.DDD.Application
{
	/// <summary>
	/// Represents the boundary of a use case in which changes to aggregates are committed together.
	/// </summary>
	/// <remarks>
	/// Repositories only register aggregates, and the unit of work writes them in a single transaction.
	/// If the unit of work is disposed without being committed, all registered changes are discarded.
	/// </remarks>
	public interface IUnitOfWork
	{
		/// <summary>
		/// Persists all registered changes in a single transaction.
		/// </summary>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>A task that represents the asynchronous commit operation.</returns>
		/// <exception cref="ConcurrencyConflictException">An aggregate has been modified by another transaction.</exception>
		Task CommitAsync(CancellationToken cancellationToken = default);
	}
}
