# Repositories and the unit of work

## Overview

| Type | Layer | Role |
|---|---|---|
| `IRepository<TAggregate, TId>` | Domain | Finds aggregates and registers them to be saved. |
| `IDeletableRepository<TAggregate, TId>` | Domain | Also registers aggregates to be deleted. |
| `IUnitOfWork` | Application | Commits all registered changes in a single transaction. |
| `ConcurrencyConflictException` | Application | Reports that another transaction modified an aggregate first. |

The domain does not know about transactions. A transaction is the boundary of a use case, which is a concern of the application layer.

## Repositories

```csharp
public interface IRepository<TAggregate, TId>
{
	Task<TAggregate?> FindAsync(TId id, CancellationToken cancellationToken = default);
	void Save(TAggregate aggregate);
}

public interface IDeletableRepository<TAggregate, TId> : IRepository<TAggregate, TId>
{
	void Delete(TAggregate aggregate);
}
```

- `FindAsync` returns `null` if the aggregate does not exist, following the .NET convention for `Find`.
- `Save` and `Delete` **only register** the aggregate. Nothing is written until `IUnitOfWork.CommitAsync` is called. This is why they are synchronous.
- `Save` does not distinguish between insert and update. The implementation decides (see below).

Define one repository interface per aggregate and add the queries that the domain needs. Do not expose `IQueryable`, which would let query logic leak out of the repository.

```csharp
public interface IMemberRepository : IRepository<Member, MemberId>
{
	Task<Member?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

### Save or delete

Implement `IDeletableRepository` only for aggregates that actually cease to exist. Whether an aggregate has `Delete` is a decision of the domain, and it is visible in the interface.

```csharp
public interface IMemberRepository : IRepository<Member, MemberId> { }   // withdrawal is a state transition
public interface ICartRepository : IDeletableRepository<Cart, CartId> { } // a cart really disappears
```

If you want to keep deleted rows for auditing, implement `Delete` as a soft delete in the infrastructure. From the domain's point of view, the aggregate is gone.

## The unit of work

```csharp
public sealed class ChangeMemberEmailUseCase(IMemberRepository members, IUnitOfWork unitOfWork, TimeProvider time)
{
	public async Task ExecuteAsync(Guid memberId, string email, CancellationToken cancellationToken)
	{
		var member = await members.FindAsync(new MemberId(memberId), cancellationToken)
			?? throw new KeyNotFoundException($"Member {memberId} was not found.");

		member.ChangeEmail(new Email(email), time);
		members.Save(member);
		await unitOfWork.CommitAsync(cancellationToken);
	}
}
```

- Repositories and the unit of work share the same connection within a DI scope (one per request).
- There is no rollback method. If the unit of work is disposed without being committed, the changes are discarded.
- Domain services do not commit. They operate on the aggregates they receive, and the application service loads, saves and commits.

## Implementing the infrastructure

### Insert or update

The implementation decides whether to insert or update by remembering which aggregates were loaded (an identity map).

```
FindAsync(id)   → remember the aggregate as "loaded"
Save(aggregate) → loaded: UPDATE, otherwise: INSERT
```

If an aggregate to be inserted already exists, the primary key constraint rejects it. **Do not use database upsert statements** such as `MERGE` or `ON CONFLICT DO UPDATE`, which would silently overwrite existing data.

### Begin the transaction on first access

To keep reading and writing in the same transaction, begin the transaction when the database is first accessed (usually in `FindAsync`), not in `CommitAsync`.

### Concurrency

Wrapping a use case in a transaction is **not** enough to prevent concurrent modifications. Under the default isolation level (READ COMMITTED), two transactions can read the same state, both pass the checks of the aggregate, and both write. For example, if two transactions each add a line to an order, both inserts succeed and the order total can exceed its limit.

The usual approach is **optimistic concurrency on the aggregate root**:

```
Save(order)
  UPDATE Orders SET ..., Token = Token + 1 WHERE Id = @Id AND Token = @TokenWhenLoaded
    → 1 row:  continue with child rows
    → 0 rows: another transaction updated it first → throw ConcurrencyConflictException
```

- **Always update the row of the root**, even if only child rows changed. This makes every change to the aggregate collide on the same row.
- Keep the token in the infrastructure (in the identity map, or as a shadow property in EF Core). The domain does not need a `Version` property.
- For aggregates with frequent conflicts (inventory, seat reservations), consider pessimistic locking instead (`SELECT ... FOR UPDATE`, or `WITH (UPDLOCK)` in SQL Server) in `FindAsync`.

When `ConcurrencyConflictException` is thrown:

- For operations by users, return HTTP 409 and ask them to reload.
- For operations that are safe to repeat (batches, event handlers), retry the whole use case from loading, with a limit on the number of attempts.

### Rules across aggregates

A rule across aggregates, such as "email addresses are unique among members", is not protected by the concurrency token of any single aggregate. Always add a unique constraint in the database as the final defense. A check in the domain is only for returning a friendly error.

### Collecting domain events

In `CommitAsync`, call `PullDomainEvents()` on each registered aggregate and write the events to the outbox in the same transaction as the aggregates. Do not react to the events here. The outbox publisher reacts to them in a transaction separate from the original change. Only when the business requires immediate consistency, you can also react to them before commit. See [Domain events](domain-events.md#when-to-dispatch).

### Example: Dapper

The following sketch shows the parts that an implementation needs. It is not a complete implementation.

```csharp
internal sealed class DapperUnitOfWork(DbConnection connection) : IUnitOfWork, IAsyncDisposable
{
	private DbTransaction? _transaction;
	private readonly Dictionary<object, long> _loaded = new(ReferenceEqualityComparer.Instance); // aggregate → token
	private readonly List<Func<DbTransaction, CancellationToken, Task>> _pending = [];

	internal async Task<DbTransaction> GetTransactionAsync(CancellationToken cancellationToken)
	{
		if (_transaction is not null) return _transaction;
		await connection.OpenAsync(cancellationToken);
		return _transaction = await connection.BeginTransactionAsync(cancellationToken);
	}

	internal void MarkLoaded(object aggregate, long token) => _loaded[aggregate] = token;
	internal bool TryGetToken(object aggregate, out long token) => _loaded.TryGetValue(aggregate, out token);
	internal void Enqueue(Func<DbTransaction, CancellationToken, Task> work) => _pending.Add(work);

	public async Task CommitAsync(CancellationToken cancellationToken = default)
	{
		var transaction = await GetTransactionAsync(cancellationToken);
		foreach (var work in _pending) await work(transaction, cancellationToken);
		// pull domain events from the registered aggregates, and write them to the outbox here (see Domain events)
		await transaction.CommitAsync(cancellationToken);
	}

	public async ValueTask DisposeAsync()
	{
		if (_transaction is not null) await _transaction.DisposeAsync(); // rolls back if not committed
		await connection.DisposeAsync();
	}
}
```

```csharp
public void Save(Order order) => _unitOfWork.Enqueue(async (transaction, cancellationToken) =>
{
	var connection = transaction.Connection!;
	if (_unitOfWork.TryGetToken(order, out var token))
	{
		var affected = await connection.ExecuteAsync(
			"UPDATE Orders SET ..., Token = Token + 1 WHERE Id = @Id AND Token = @Token",
			new { Id = order.Id.Value, Token = token }, transaction);
		if (affected == 0)
		{
			throw new ConcurrencyConflictException(typeof(Order), order.Id);
		}
	}
	else
	{
		await connection.ExecuteAsync("INSERT INTO Orders (..., Token) VALUES (..., 1)", ..., transaction);
	}
	// save child rows; deleting and re-inserting them is the simplest approach
});
```

### Notes for EF Core

- Ignore `DomainEvents` in the model configuration.
- A `rowversion` column on the root is not updated when only child rows change. Touch the root explicitly so that conflicts are detected.
- `Save` can add untracked aggregates and do nothing for tracked ones.

## Logical locks

Some data needs to be locked for a long time while a user works on it. Where the lock belongs depends on its purpose.

| Purpose | Example | Where |
|---|---|---|
| The lock has a business meaning | "Cannot be edited during approval" | The state of the aggregate, with operations such as `Lock` and `Unlock` |
| The lock prevents simultaneous editing in the UI | "Alice is editing this record" | A separate service in the application layer |

In both cases, give the lock an expiry, provide a way for administrators to release it, and keep optimistic concurrency as the final defense.
