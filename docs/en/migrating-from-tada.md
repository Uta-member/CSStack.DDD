# Migrating from CSStack.TADA

CSStack.DDD is the successor to CSStack.TADA. This guide explains how to move a project from TADA to CSStack.DDD.

The guide is written against TADA 4.0.0. If you use an older version, read [Migrating from older versions](#migrating-from-older-versions) first. You do not need to upgrade to TADA 4.0.0 before migrating, because the code that differs between TADA versions is the code you rewrite anyway.

## Before you start

- **The way transactions are expressed is the opposite.** TADA passes a session to every repository call inside `ExecuteTransactionAsync`. In CSStack.DDD, repositories and the unit of work share a connection within a DI scope, and the application service commits at the end. Application services have to be rewritten, not renamed, but the result is shorter.
- **Validation moves from exceptions to errors.** `Validate()` returns a list of errors, and operations of entities throw. See [Validation](validation.md).
- **Some of what TADA provides is not provided.** There are no aggregate services, domain service interfaces, command or query service interfaces, `Optional<T>` or transaction manager. The reasons are in [Design decisions](design-decisions.md#what-the-toolkit-does-not-provide).
- **Migrate from the bottom layer up.** After you replace the package, every `using CSStack.TADA;` fails to compile. Fix the domain layer first, then the application layer, the infrastructure and the composition root, and build after each layer.

## What changes

| Concern | CSStack.TADA | CSStack.DDD |
|---|---|---|
| Transactions | `ITransactionManager.ExecuteTransactionAsync` wraps the use case in a lambda and passes sessions | Repositories register changes; `IUnitOfWork.CommitAsync` commits them |
| Validation | `Validate()` throws (or `Create` throws) | `Validate()` returns errors; operations throw `DomainValidationException` |
| Creation and reconstruction | `Create` and `Reconstruct` on value objects and entities | On entities only. Value objects have a single constructor |
| Saving | `SaveAsync` is an upsert | `Save` registers; the infrastructure decides insert or update from what it loaded |
| Not found | `Optional<T>.Empty` | `null` |
| Domain events | Not provided | `AggregateRoot` records them; the unit of work pulls them on commit |
| Who performed an operation | `TOperateInfo` passed to `SaveAsync` | Business "who": an argument of the operation. Audit "who": recorded by the infrastructure |
| Concurrency | Not provided | `ConcurrencyConflictException`, detected by the infrastructure |

## Type mapping (TADA 4.0.0)

| CSStack.TADA | CSStack.DDD | Notes |
|---|---|---|
| `using CSStack.TADA;` | `using CSStack.DDD.Domain;` / `using CSStack.DDD.Application;` | One namespace per layer |
| `IValueObject` | `IValueObject<TSelf>` | Records satisfy it automatically |
| `IValueObject.Validate()` (`void`, throws) | `IValidatable.Validate()` (returns `IReadOnlyList<ValidationError>`) | `IValidatable` is a separate interface |
| `ISingleValueObject<TValue>` | `ISingleValueObject<TSelf, TValue>` | `TSelf` is added **first** |
| `ISingleValueObject<TValue, TSelf>` | `ISingleValueObject<TSelf, TValue>` | The order of the type parameters is **reversed**. `Create` and `Reconstruct` are not required |
| `IEntity<TIdentifier>` / `EntityBase<TSelf, TIdentifier>` | `IEntity<TSelf, TId>` / `Entity<TSelf, TId>` | `TId` must implement `IEquatable<TId>` |
| `Identifier` | `Id` | |
| `IEntity.Validate()` | None | Enforce in operations instead |
| None | `AggregateRoot<TSelf, TId>`, `IDomainEvent`, `IDomainEventSource` | Replace your own base classes if you wrote them |
| `IAggregateService<...>` / `AggregateServiceBase<...>` | None | Operations of the entity, plus the application service |
| `IRepository<TEntity, TId, TOperateInfo, TSession>` | `IRepository<TAggregate, TId>` | No session or operation info |
| `IRepositoryDeletable<...>` | `IDeletableRepository<TAggregate, TId>` | |
| `FindByIdentifierAsync(session, id, ct)` → `ValueTask<Optional<T>>` | `FindAsync(id, ct)` → `Task<T?>` | |
| `SaveAsync(session, entity, operateInfo, ct)` | `Save(aggregate)` | Synchronous; registers only |
| `DeleteAsync(session, entity, operateInfo, ct)` | `Delete(aggregate)` | Synchronous; registers only |
| `ITransactionManager`, `TransactionManager`, `TransactionSessions` | `IUnitOfWork` | |
| `ITransactionService<TSession>` | Your implementation of `IUnitOfWork` | Infrastructure |
| Rollback on exception | Dispose without committing | No rollback method |
| `ICommandService<...>`, `ICommandServiceDTO`, `IQueryService<...>`, `IQueryServiceDTO` | None | Declare `ExecuteAsync` in your own interfaces |
| `Optional<T>`, `OptionalExtensions` | None | See [Partial updates without `Optional<T>`](#step-5-partial-updates-without-optionalt) |
| `TADAException`, `NestedTransactionException`, `TransactionSessionNotFoundException` | None | |
| None | `ValidationError`, `Required`, `InvalidFormat`, `TooLong`, `TooShort`, `OutOfRange<T>`, `DomainException`, `DomainValidationException` | |
| None | `ConcurrencyConflictException` | |

## Step 1: Replace the package

```xml
<!-- Before -->
<PackageReference Include="CSStack.TADA" Version="4.0.0" />
<!-- After -->
<PackageReference Include="CSStack.DDD" Version="x.y.z" />
```

Then replace `using CSStack.TADA;` with `using CSStack.DDD.Domain;` in the domain layer, and add `using CSStack.DDD.Application;` only where the unit of work is used.

## Step 2: Value objects

```csharp
// Before (TADA)
public sealed record Email : ISingleValueObject<string, Email>
{
	public string Value { get; }

	private Email(string value)
	{
		Value = value;
	}

	public static Email Create(string value)
	{
		var email = new Email(value);
		email.Validate();
		return email;
	}

	public static Email Reconstruct(string value) => new(value);

	public void Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) throw new ArgumentException("The email address is required.");
		if (!Value.Contains("@")) throw new ArgumentException("The email address is invalid.");
	}
}

// After (CSStack.DDD)
public sealed record Email : ISingleValueObject<Email, string>, IValidatable
{
	public string Value { get; }

	public Email(string value)
	{
		Value = value ?? throw new ArgumentNullException(nameof(value));
	}

	public IReadOnlyList<ValidationError> Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) return [new Required()];
		if (!Value.Contains("@")) return [new InvalidFormat()];
		return [];
	}
}
```

- Swap the type parameters: `ISingleValueObject<string, Email>` becomes `ISingleValueObject<Email, string>`.
- Declare `IValidatable` as well. `ISingleValueObject` does not include it.
- Return errors instead of throwing. Choose the error type that describes the problem (`Required`, `TooLong`, and so on), or derive your own from `ValidationError`.
- Replace both `Email.Create(x)` and `Email.Reconstruct(x)` with `new Email(x)`. Creating a value no longer validates it. The entity validates it when the value is used to create or change the entity (step 3), so check that every place that called `Create` now passes the value to such an operation.
- A `required` `init` property instead of a constructor also satisfies `Value { get; }`. If you keep it to avoid changing callers, report `null` as `Required` in `Validate()`, because there is no constructor to reject it.

See [Value objects](value-objects.md).

## Step 3: Entities and aggregate roots

```csharp
// Before (TADA)
public sealed class Member : EntityBase<Member, MemberId>
{
	public override MemberId Identifier { get; }
	public Email Email { get; private set; }

	private Member(MemberId identifier, Email email)
	{
		Identifier = identifier;
		Email = email;
	}

	public static Member Create(MemberId identifier, Email email)
	{
		var member = new Member(identifier, email);
		member.Validate();
		return member;
	}

	public static Member Reconstruct(MemberId identifier, Email email) => new(identifier, email);

	public void ChangeEmail(Email email)
	{
		email.Validate();
		Email = email;
	}

	public override void Validate()
	{
		Email.Validate();
	}
}

// After (CSStack.DDD)
public sealed class Member : AggregateRoot<Member, MemberId>
{
	public override MemberId Id { get; }
	public Email Email { get; private set; }

	private Member(MemberId id, Email email)
	{
		Id = id;
		Email = email;
	}

	public static Member Register(Email email, DateTimeOffset occurredAt)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid(); // validate before creating
		var member = new Member(MemberId.New(), email);
		member.AddDomainEvent(new MemberRegistered(Guid.NewGuid(), occurredAt, member.Id, email));
		return member;
	}

	public static Member Reconstruct(MemberId id, Email email) => new(id, email);

	public void ChangeEmail(Email email, DateTimeOffset occurredAt)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		Email = email;
		AddDomainEvent(new MemberEmailChanged(Guid.NewGuid(), occurredAt, Id, email));
	}
}
```

- Derive aggregate roots from `AggregateRoot<TSelf, TId>`, and entities inside an aggregate from `Entity<TSelf, TId>`.
- Rename `Identifier` to `Id`. If the entity has a property with a business name such as `MemberId`, keep it and write `public override MemberId Id => MemberId;`.
- `TId` must implement `IEquatable<TId>`. Identifiers implemented as records already do.
- Remove the public `Validate()`. Validate the input in each operation, and collect the errors of several inputs with `Under` before calling `ThrowIfInvalid()` once. See [Validation](validation.md#the-gate).
- Validate **before** creating the entity, not after.
- If you used `Validate()` to check data after reconstruction, implement `IValidatable` on the entity and return the errors instead of throwing.
- Keep `Reconstruct` free of validation and events.

See [Entities and aggregates](entities-and-aggregates.md).

## Step 4: Domain events

TADA does not provide domain events. If you wrote your own aggregate root base class and event interface, delete them and use `AggregateRoot<TSelf, TId>` and `IDomainEvent`.

- `IDomainEvent.OccurredAt` is a `DateTimeOffset`. A `DateTime` converts implicitly, but a `DateTime` whose `Kind` is `Local` or `Unspecified` gets the local offset. Make sure the times you pass are UTC, or change them to `DateTimeOffset`.
- Do not clear the events in the application service. Remove code such as `outbox.AddAsync(session, aggregate.DomainEvents)` followed by `ClearDomainEvents()`. The unit of work pulls the events on commit with `PullDomainEvents()` (step 9).
- `DomainEvents` is a read-only view. It is empty after `PullDomainEvents()`, so copy it if you need to keep the events.

See [Domain events](domain-events.md).

## Step 5: Partial updates without `Optional<T>`

CSStack.DDD has no `Optional<T>`. Where you used it, the replacement depends on what it meant.

| Where | Replacement |
|---|---|
| The return value of `FindByIdentifierAsync` | `FindAsync` returns `null` |
| An `Update(UpdateReq)` operation whose request has an `Optional<T>` per field ("not specified" means "do not change") | Split it into one operation per field (`ChangeName`, `ChangeEmail`, ...) |
| A request DTO of a command that changes several fields in one transaction | A three-state type of your own, used only in the application layer |

**Do not replace `Optional<T>` with a nullable type** (`string?`). It works while no property accepts `null`, but as soon as one does, "change to `null`" and "do not change" can no longer be told apart.

Splitting the operation follows the guideline [prefer intent over overwriting](entities-and-aggregates.md#operations). Calling an operation means "change it", so its arguments do not need a "not specified" state.

```csharp
// Before (TADA)
public void Update(UpdateReq req) // UpdateReq { Optional<MemberName> NameOptional; Optional<Email> EmailOptional; }

// After
public void ChangeName(MemberName name, DateTimeOffset occurredAt)
public void ChangeEmail(Email email, DateTimeOffset occurredAt)
```

If a command still needs to change several fields in one request, keep a three-state type in its request DTO and call only the operations for the fields that were specified. Do not use the type in the domain layer.

```csharp
if (req.Name.TryGetValue(out var name))
{
	member.ChangeName(new MemberName(name), req.OccurredAt);
}
if (req.Email.TryGetValue(out var email))
{
	member.ChangeEmail(new Email(email), req.OccurredAt);
}
members.Save(member);
await unitOfWork.CommitAsync(cancellationToken);
```

A small `readonly struct` with `HasValue`, `TryGetValue` and an implicit conversion from `T` is usually enough. Note the trade-offs:

- One event is raised per changed field instead of one event for the whole update.
- If several operations fail validation, only the errors of the first are reported. To report all of them, validate the request first. See [Validating input with the names of your form](validation.md#validating-input-with-the-names-of-your-form).
- If a later operation throws, nothing is committed, so no field is changed partially.

## Step 6: Repositories

```csharp
// Before (TADA)
public interface IMemberRepository<TSession> : IRepository<Member, MemberId, OperateInfo, TSession>
	where TSession : IDisposable
{
}

// After (CSStack.DDD)
public interface IMemberRepository : IRepository<Member, MemberId>
{
	Task<Member?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

- The session and operation info type parameters are gone, so `<TSession>` also disappears from the services that use the repository.
- The repository may have the queries that the domain needs, such as `FindByEmailAsync`. Return `Task` to match `FindAsync`.
- Derive from `IDeletableRepository` only for aggregates that really cease to exist.

Aggregate services (`IAggregateService` and `AggregateServiceBase`) have no equivalent. Move "find, change and save" into an operation of the entity and the application service (step 7). Replace `GetEntityByIdentifierAsync` with `FindAsync`.

See [Repositories and the unit of work](repositories-and-unit-of-work.md).

## Step 7: Application services

This is where the code changes the most.

```csharp
// Before (TADA)
public sealed class ChangeMemberEmailCommandService<TMemberSession>(
	ITransactionManager transactionManager,
	IMemberRepository<TMemberSession> members) : IChangeMemberEmailCommandService
	where TMemberSession : IDisposable
{
	public async ValueTask ExecuteAsync(IChangeMemberEmailCommandService.Req req, CancellationToken cancellationToken = default)
	{
		await transactionManager.ExecuteTransactionAsync<TMemberSession>(
			async (sessions, ct) =>
			{
				var session = sessions.GetSession<TMemberSession>();
				var found = await members.FindByIdentifierAsync(session, req.MemberId, ct);
				if (!found.TryGetValue(out var member))
				{
					throw new MemberNotFoundException(req.MemberId);
				}

				member.ChangeEmail(Email.Create(req.Email));
				await members.SaveAsync(session, member, req.OperateInfo, ct);
			},
			cancellationToken: cancellationToken);
	}
}

// After (CSStack.DDD)
public sealed class ChangeMemberEmailCommandService(
	IMemberRepository members,
	IUnitOfWork unitOfWork) : IChangeMemberEmailCommandService
{
	public async ValueTask ExecuteAsync(IChangeMemberEmailCommandService.Req req, CancellationToken cancellationToken = default)
	{
		var member = await members.FindAsync(req.MemberId, cancellationToken)
			?? throw new MemberNotFoundException(req.MemberId);

		member.ChangeEmail(new Email(req.Email), req.OccurredAt);
		members.Save(member);
		await unitOfWork.CommitAsync(cancellationToken);
	}
}
```

- The lambda, the sessions and the `<TSession>` type parameter are gone.
- If an exception is thrown, `CommitAsync` is not reached, and the unit of work is disposed with the DI scope. The registered changes are discarded, which replaces TADA's rollback. Logic in `beforeRollbackHandler` moves to a `try`/`catch` in the application service, or to global exception handling.
- **Do not call one application service from another.** TADA rejected this with `NestedTransactionException`. In CSStack.DDD it compiles and runs, but the inner `CommitAsync` commits in the middle of the outer use case. Move the shared logic into a domain service or the entity.

### Domain services

TADA up to 3.x provided `IDomainService<TReq>` with the session in its request DTO. In CSStack.DDD, a domain service is a plain class (and interface, if you like) without a session. It does not commit. The application service loads, saves and commits. If a domain service needs to look something up, such as checking that an email address is not used, it can take a repository interface.

### Several data stores

TADA told data stores apart by the session type (`ExecuteTransactionAsync<TUserSession, TOrderSession>`). `IUnitOfWork` has only one type, so derive an interface per data store in the application layer.

```csharp
public interface IMemberUnitOfWork : IUnitOfWork { }
public interface IOrderUnitOfWork : IUnitOfWork { }
```

With a single data store, use `IUnitOfWork` as it is. Committing two units of work is not atomic, just like committing two sessions in TADA. Prefer changing one data store per use case and reaching the other through domain events.

### Event handlers

If your handlers began their own transaction with `ExecuteTransactionAsync`, they now register their changes and call `CommitAsync` on the unit of work of their own scope. See [Reacting to events](domain-events.md#reacting-to-events).

## Step 8: Who performed the operation

TADA passed `TOperateInfo` to `SaveAsync` and `DeleteAsync`, typically to fill audit columns. CSStack.DDD separates the two kinds of "who" ([Who performed an operation](entities-and-aggregates.md#who-performed-an-operation)).

- **"Who" with a business meaning** (the author, the approver): pass it as an argument of the operation. You can keep your `OperateInfo` in request DTOs for this.
- **"Who" for auditing** (`UpdatedBy`, `UpdatedAt`): record it in the infrastructure. Set an execution context (for example, a scoped object holding the user and the time) at the entry point, such as the request pipeline or the command line, and let the repositories and the unit of work read it. The domain and application layers do not read it.

If events are dispatched in a separate scope through an outbox, write the actor to the envelope of the outbox record, and restore the execution context from it in the scope that runs the handlers ([Event metadata](domain-events.md#event-metadata)).

## Step 9: The infrastructure

Replace your implementations of `ITransactionService<TSession>` with an implementation of `IUnitOfWork`. [Implementing the infrastructure](repositories-and-unit-of-work.md#implementing-the-infrastructure) shows what it needs. The points that differ from TADA:

| Concern | TADA | CSStack.DDD |
|---|---|---|
| Beginning the transaction | `BeginAsync` before the lambda runs | On the first access to the database, so reading and writing are in the same transaction |
| Insert or update | `SaveAsync` is an upsert | Remember which aggregates were loaded (an identity map). **Do not use upsert statements**, which silently overwrite existing data |
| Domain events | Written by the application service | Pulled from the registered aggregates and written to the outbox in `CommitAsync` |
| Rollback | `RollbackAsync` | Dispose without committing |
| Concurrency | Not provided | Optimistic concurrency on the aggregate root; throw `ConcurrencyConflictException` |

Register the services with the DI container as follows.

- The unit of work and the repositories are **scoped**, because the repositories depend on the unit of work of the scope. Repositories that were singletons in TADA must become scoped. `ValidateScopes = true` finds the ones you missed.
- Register the implementation once and forward the interfaces to it, so that the application layer and the repositories share the same instance within a scope.

```csharp
services.AddScoped<MemberUnitOfWork>();
services.AddScoped<IMemberUnitOfWork>(sp => sp.GetRequiredService<MemberUnitOfWork>());
services.AddScoped<IMemberRepository, MemberRepository>(); // depends on MemberUnitOfWork
```

- Remove the registrations of `TransactionManager` and `ITransactionService<TSession>`.

## Step 10: Command and query service interfaces

CSStack.DDD has no `ICommandService`, `IQueryService` or their DTO markers. Declare `ExecuteAsync` in each interface. You can keep nesting the request and response DTOs.

```csharp
// Before (TADA)
public interface IChangeMemberEmailCommandService : ICommandService<IChangeMemberEmailCommandService.Req>
{
	public sealed record Req : ICommandServiceDTO { ... }
}

// After
public interface IChangeMemberEmailCommandService
{
	ValueTask ExecuteAsync(Req req, CancellationToken cancellationToken = default);

	public sealed record Req { ... }
}
```

If your code relied on the common interfaces (for example, as generic constraints), define equivalent interfaces in your application.

## Step 11: Exceptions

| CSStack.TADA | CSStack.DDD |
|---|---|
| Exceptions thrown by `Validate()` or `Create` | `DomainValidationException`, whose `Errors` contain typed errors with paths |
| `ValueObjectNullException`, `ValueObjectLengthException`, `ValueObjectInvalidException` (up to 3.x) | `Required`, `TooShort`/`TooLong`, `InvalidFormat`, reported by `Validate()` |
| `ObjectNotFoundException`, `ObjectAlreadyExistException`, `DomainInvalidOperationException` (up to 3.x) | Your own exceptions. Derive the exceptions for business rules from `DomainException` |
| `NestedTransactionException`, `TransactionSessionNotFoundException` | None |
| None | `ConcurrencyConflictException` |

Handle `DomainException` (including `DomainValidationException`) and `ConcurrencyConflictException` once, in the presentation layer. Errors do not contain messages, so turn them into messages there. See [Validation](validation.md#turning-errors-into-messages).

## Changes that compile but behave differently

| Change | Effect |
|---|---|
| `Save` is not an upsert | Saving an aggregate that was not loaded, when a row with the same identifier exists, fails on the primary key instead of overwriting it |
| Entity equality uses `is TSelf` | In TADA 3.0 and later, entities of different runtime types are never equal. In CSStack.DDD, two classes derived from the same `TSelf` are equal if their identifiers are equal. The hash code no longer includes the type |
| `FindAsync` returns `null` | Code that checked `HasValue` must check for `null` |
| Validation exceptions | Exceptions such as `ArgumentException` become `DomainValidationException` |
| `OccurredAt` is a `DateTimeOffset` | A local or unspecified `DateTime` gets the local offset |
| Nested use cases | No longer rejected; the inner use case commits in the middle |

## Migrating from older versions

The repository, transaction and entity shapes are the same in every TADA version, so steps 1 and 3 to 11 apply as they are. The differences are mostly in validation.

Find the rows that include your version.

| TADA versions | What differs from 4.0.0 | What to do |
|---|---|---|
| 2.0.1 to 3.0.x | `IValueObject` and `IEntity` have no `Validate()`. Rules are checked in `Create` | Move the checks from `Create` into `Validate()`, returning errors (step 2) |
| 3.0.x and earlier | `IDomainService<TReq>`, `IDomainServiceDTO`, `ILengthDefinedSingleValueObject` and the exceptions in step 11 exist | Domain services: see [Domain services](#domain-services). Length limits: keep them as `static` members of the value object, or as constants used by `Validate()` |
| 2.0.x and earlier | The lambda of `ExecuteTransactionAsync` does not take a `CancellationToken`, and entity equality does not compare the runtime type | The lambda is removed in step 7, so the first difference does not matter. For derived types, CSStack.DDD equality behaves like these versions |
| 1.x and 0.x | `ValueObjectBase` (an abstract record with `Validate()` and `IsInvalidValue`), `IEntity.IsInvalidValue`, `ValidateHelper`, `KeyedValidateHelper<TKey>`, `MultiReasonException` and `KeyedMultiReasonException<TKey>`. `ICommandServiceWithRes`, `IQueryServiceWithoutReq` and `IDomainServiceWithRes` | Implement `IValueObject<TSelf>` and `IValidatable` instead of deriving from `ValueObjectBase`. Replace `IsInvalidValue` with `Validate().Count > 0`. Collect `ValidationError`s in a list instead of using the helpers, and use `Under` where you used keys. Replace the `WithRes` and `WithoutReq` interfaces as in step 10 |
| 1.x and 0.x | `ISingleValueObject<TValue>` does not exist (only `ISingleValueObject<TValue, TSelf>`). Before 1.0.4, `IRepositoryDeletable` does not exist either | Nothing extra. Use `ISingleValueObject<TSelf, TValue>`, and `IDeletableRepository` for aggregates that are deleted |

## Checklist

- [ ] The package and namespaces are replaced, and nothing references `CSStack.TADA`.
- [ ] Value objects report errors from `Validate()`, and their constructors check only what the type needs to exist.
- [ ] Every operation that creates or changes an entity validates its input with `ThrowIfInvalid()`.
- [ ] `Reconstruct` does not validate or raise events.
- [ ] No `Optional<T>` was replaced with a nullable type where `null` is a valid value.
- [ ] Application services call `Save` and `CommitAsync`, and do not call other application services.
- [ ] The unit of work pulls domain events on commit, and the application layer does not clear them.
- [ ] Audit columns are filled by the infrastructure from an execution context.
- [ ] Repositories decide insert or update from what they loaded, without upsert statements.
- [ ] The unit of work and the repositories are registered as scoped.
- [ ] `DomainException` (including `DomainValidationException`) and `ConcurrencyConflictException` are handled in the presentation layer.
