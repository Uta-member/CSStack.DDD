using CSStack.DDD.Domain;

namespace CSStack.DDD.Application
{
	/// <summary>
	/// Provides extension methods for using repositories in use cases.
	/// </summary>
	public static class RepositoryExtensions
	{
		/// <summary>
		/// Gets the aggregate with the specified identifier, and throws if it does not exist.
		/// </summary>
		/// <remarks>
		/// Use this method when the aggregate is expected to exist, such as when a use case operates on an aggregate specified by the caller.
		/// Use <see cref="IRepository{TAggregate, TId}.FindAsync"/> when a missing aggregate is a normal result.
		/// This method is in the application layer because deciding that a missing aggregate is an error is a concern of the use case.
		/// </remarks>
		/// <typeparam name="TAggregate">The type of the aggregate root.</typeparam>
		/// <typeparam name="TId">The type of the identifier of the aggregate root.</typeparam>
		/// <param name="repository">The repository to get the aggregate from.</param>
		/// <param name="id">The identifier of the aggregate.</param>
		/// <param name="cancellationToken">A token to cancel the operation.</param>
		/// <returns>The aggregate with the specified identifier.</returns>
		/// <exception cref="AggregateNotFoundException">The aggregate does not exist.</exception>
		public static async Task<TAggregate> GetAsync<TAggregate, TId>(
			this IRepository<TAggregate, TId> repository,
			TId id,
			CancellationToken cancellationToken = default)
			where TAggregate : class, IAggregateRoot<TAggregate, TId>
			where TId : notnull, IEquatable<TId>
		{
			return await repository.FindAsync(id, cancellationToken).ConfigureAwait(false)
				?? throw new AggregateNotFoundException(typeof(TAggregate), id);
		}
	}
}
