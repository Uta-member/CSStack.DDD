namespace CSStack.DDD.Domain
{
	/// <summary>
	/// Base class for entities that enforces equality by identifier.
	/// </summary>
	/// <remarks>
	/// <see cref="Equals(object?)"/> and <see cref="GetHashCode"/> are sealed so that derived classes cannot change how identity is compared.
	/// The identifier should be assigned when the entity is created (for example, generated in the domain)
	/// so that it never changes after the entity is persisted.
	/// </remarks>
	/// <typeparam name="TSelf">The derived entity type.</typeparam>
	/// <typeparam name="TId">The type of the identifier.</typeparam>
	public abstract class Entity<TSelf, TId> : IEntity<TSelf, TId>
		where TSelf : Entity<TSelf, TId>
		where TId : notnull, IEquatable<TId>
	{
		/// <inheritdoc/>
		public abstract TId Id { get; }

		/// <summary>
		/// Determines whether the specified entity has the same identifier as this entity.
		/// </summary>
		/// <param name="other">The entity to compare with this entity.</param>
		/// <returns><see langword="true"/> if the specified entity has the same identifier; otherwise, <see langword="false"/>.</returns>
		public bool Equals(TSelf? other)
		{
			return other is not null && (ReferenceEquals(this, other) || Id.Equals(other.Id));
		}

		/// <summary>
		/// Determines whether the specified object is an entity of the same type with the same identifier.
		/// </summary>
		/// <param name="obj">The object to compare with this entity.</param>
		/// <returns><see langword="true"/> if the specified object is an entity of the same type with the same identifier; otherwise, <see langword="false"/>.</returns>
		public sealed override bool Equals(object? obj)
		{
			return obj is TSelf other && Equals(other);
		}

		/// <summary>
		/// Returns a hash code derived from the identifier.
		/// </summary>
		/// <returns>The hash code of the identifier.</returns>
		public sealed override int GetHashCode()
		{
			return Id.GetHashCode();
		}

		/// <summary>
		/// Determines whether two entities are equal by their identifiers.
		/// </summary>
		/// <param name="left">The first entity to compare.</param>
		/// <param name="right">The second entity to compare.</param>
		/// <returns><see langword="true"/> if the entities have the same identifier; otherwise, <see langword="false"/>.</returns>
		public static bool operator ==(Entity<TSelf, TId>? left, Entity<TSelf, TId>? right)
		{
			return left is null ? right is null : left.Equals(right);
		}

		/// <summary>
		/// Determines whether two entities are not equal by their identifiers.
		/// </summary>
		/// <param name="left">The first entity to compare.</param>
		/// <param name="right">The second entity to compare.</param>
		/// <returns><see langword="true"/> if the entities have different identifiers; otherwise, <see langword="false"/>.</returns>
		public static bool operator !=(Entity<TSelf, TId>? left, Entity<TSelf, TId>? right)
		{
			return !(left == right);
		}

		static bool IEntity<TSelf, TId>.operator ==(TSelf? left, TSelf? right)
		{
			return left == right;
		}

		static bool IEntity<TSelf, TId>.operator !=(TSelf? left, TSelf? right)
		{
			return left != right;
		}
	}
}
