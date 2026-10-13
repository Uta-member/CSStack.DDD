# Domain events

A domain event is a fact that something meaningful to the business has happened, such as "a member has registered".

## Defining events

```csharp
public sealed record MemberRegistered(Guid EventId, DateTimeOffset OccurredAt, MemberId MemberId, Email Email) : IDomainEvent;
```

| Property | Purpose |
|---|---|
| `EventId` | A unique identifier. Consumers use it to ignore duplicate deliveries. |
| `OccurredAt` | When the event occurred. |
| The aggregate identifier | Which aggregate the event happened in. |
| Business data | What consumers need to react to the event. |

Guidelines:

- **Name events in the past tense**, such as `MemberRegistered` or `EmailChanged`.
- **Make events immutable records.** A fact does not change after it has happened.
- **Do not reference the aggregate itself.** The aggregate is mutable, so copy the values that consumers need.
- **Get the time from `TimeProvider`**, not from `DateTimeOffset.UtcNow`, so that tests can control it.

## Recording events

Aggregate roots that derive from `AggregateRoot<TSelf, TId>` record events with `AddDomainEvent`.

```csharp
public void ChangeEmail(Email email, TimeProvider time)
{
	email.Validate().Under(nameof(Email)).ThrowIfInvalid();
	Email = email;
	AddDomainEvent(new MemberEmailChanged(Guid.NewGuid(), time.GetUtcNow(), Id, email));
}
```

Do not record events in the reconstruction factory. Reconstruction restores past state; nothing new has happened.

## Why events are not dispatched immediately

Events are recorded in the aggregate and dispatched later, when changes are committed.

1. If the transaction fails after the operation, an event dispatched immediately would describe a change that never happened.
2. The aggregate does not need to depend on a dispatcher. It only records what happened.

## Collecting events

The infrastructure collects events from all aggregates registered with the unit of work by calling `PullDomainEvents()`.

```csharp
foreach (var aggregate in registeredAggregates)
{
	if (aggregate is IDomainEventSource source)
	{
		foreach (var domainEvent in source.PullDomainEvents())
		{
			// write to the outbox
		}
	}
}
```

- `IDomainEventSource` is non-generic, so events can be collected from aggregates of any type without reflection.
- `PullDomainEvents()` returns the events and clears them in one operation, so the same event is never dispatched twice by mistake.

## When to dispatch

| Timing | Suitable for | Caveat |
|---|---|---|
| **Outbox pattern (recommended)** | Updating other aggregates (eventual consistency), side effects such as sending email, integration with other systems | Delivery is at least once, so consumers must ignore duplicate deliveries |
| Before commit, in the same transaction | Cases where the business requires immediate consistency and you deliberately modify several aggregates in one transaction | If the reaction fails, the original change is rolled back too. The transaction grows larger |
| Directly after commit | Not recommended | Events are lost if the process stops before dispatching |

The outbox pattern is the default. The work is split into two transactions.

```
Transaction 1 (the use case)
  Update the aggregate + write the events to the outbox → commit
  * Nothing reacts to the events yet

Transaction 2 (the outbox publisher, per event)
  Fetch the event from the outbox → react to it → the changes made by the reaction + the processed mark → commit
  * On failure, roll back and retry. The changes of transaction 1 are not affected
```

If a reaction fails, the change made by the original use case is not undone. The reaction's changes are applied later by retrying (eventual consistency).

When reacting within the transaction, an exception thrown there rolls back the original change as well. For example, if opening a point account fails, the member registration fails too. Choose it deliberately, only when the business requires it.

## Writing to the outbox in the unit of work

`CommitAsync` collects events from the registered aggregates and writes them to the outbox in the same transaction as the aggregates.

```csharp
public async Task CommitAsync(CancellationToken cancellationToken = default)
{
	var transaction = await GetTransactionAsync(cancellationToken);

	var domainEvents = _registeredAggregates.SelectMany(aggregate => aggregate.PullDomainEvents()).ToList();

	foreach (var work in _pending) await work(transaction, cancellationToken);
	await WriteOutboxAsync(domainEvents, transaction, cancellationToken); // written in the same transaction as the aggregates
	await transaction.CommitAsync(cancellationToken);
}
```

- `_registeredAggregates` is a `List<IDomainEventSource>`.
- If `CommitAsync` fails, neither the changes to the aggregates nor the events are written. A change that never happened is never dispatched.

## Reacting to events

The toolkit does not provide a handler interface for reacting to events, nor a dispatcher that resolves and invokes handlers. How handlers are grouped, in which transaction they run and how they are retried depends on how the application is built. Use your own mechanism, or a library such as MediatR, Wolverine or MassTransit.

Whichever mechanism you use, follow these rules:

- **The outbox relay in the infrastructure layer watches the outbox and dispatches the events.** The host runs it as a `BackgroundService` or as a separate worker process. Neither the application service that saved the aggregate nor the presentation layer dispatches the events.
- **React to each event in a scope, unit of work and transaction separate from the original change.** Commit the reaction's changes together with the processed mark, in the same transaction.
- **Make reactions idempotent.** Events are delivered at least once. Database changes committed in the same transaction as the processed mark are not applied twice, but checking for existence by a business key, with a unique constraint as the last line of defense, is safer. External side effects such as sending email are repeated if the commit fails afterward, so ignore duplicates by `EventId`.
- **Retry on failure, and dead-letter the event after a limit.**

The following sketch shows the parts an outbox relay needs. It is not a complete implementation.

```csharp
internal sealed class OutboxRelay(IServiceScopeFactory scopes, OutboxStore outbox) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			foreach (var record in await outbox.FetchPendingAsync(100, stoppingToken))
			{
				try
				{
					// a scope per event, so the unit of work and the transaction are separate from the original change
					await using var scope = scopes.CreateAsyncScope();
					var unitOfWork = scope.ServiceProvider.GetRequiredService<DapperUnitOfWork>();

					await ReactAsync(scope.ServiceProvider, record.Deserialize(), stoppingToken); // how handlers are invoked is up to the application
					unitOfWork.Enqueue((transaction, cancellationToken) => outbox.MarkProcessedAsync(record.Id, transaction, cancellationToken));
					await unitOfWork.CommitAsync(stoppingToken); // commits the reaction's changes together with the processed mark
				}
				catch (Exception exception) when (exception is not OperationCanceledException)
				{
					await outbox.RecordFailureAsync(record.Id, exception, stoppingToken); // count the attempt, and dead-letter it after the limit
				}
			}

			await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
		}
	}
}
```

- When a reaction saves another aggregate, its events are written to the outbox by the same `CommitAsync` and dispatched in a later round. A chain of events is also processed one step at a time, each in its own transaction.
- If an event has several handlers that share one transaction, a failure in one rolls back all of them, and they are retried together. To make handlers independent of each other, split outbox records per handler (subscriber), or use a separate queue per subscriber in a message broker.
- When running several relay instances, lock records as you fetch them so that the same record is not processed twice (`SELECT ... FOR UPDATE SKIP LOCKED`, or `WITH (UPDLOCK, READPAST)` on SQL Server).
- With a message broker, the relay only sends outbox records to the broker. The consumer that receives them from the broker reacts in the same way (create a scope, react, commit).

### Reacting within the transaction (when immediate consistency is required)

When the business requires immediate consistency, you can also react to events in `CommitAsync`, before commit.

- A reaction that saves another aggregate raises new events. Repeat collecting and reacting until no events remain, with a limit on the number of rounds so that events that keep raising each other do not loop forever.
- If a reaction throws an exception, the original change is rolled back too.
- React here only where immediate consistency is required. Keep writing events to the outbox for everything handled with eventual consistency.

## Event metadata

Information such as who performed the operation or which request caused it is metadata, not part of the business fact. Add it to an envelope when writing to the outbox, instead of adding it to every event.

```
Outbox record
├─ Envelope (added by the infrastructure): EventType, ActorId, CorrelationId, ...
└─ Body (created by the domain): MemberRegistered { MemberId, Email, ... }
```

If "who" has a business meaning (for example, the approver), include it in the event itself. See [Entities and aggregates](entities-and-aggregates.md#who-performed-an-operation).

## Domain events and integration events

Domain events are used within the same process, so they may contain value objects. Events sent to other systems are a contract with those systems. Convert them into separate integration event types that contain only primitive values, so that changes to the domain model do not break the contract.

## Multiple aggregates

A common guideline is to modify **one aggregate per transaction** and to update other aggregates through domain events (eventual consistency). Handlers that update other aggregates then run from the outbox, in a transaction separate from the original change (see [When to dispatch](#when-to-dispatch)). Modifying several aggregates in one transaction is possible with the unit of work, and is a reasonable choice when the business requires immediate consistency, but choose it deliberately. In that case, if the change to any of the aggregates fails, all changes are rolled back.
