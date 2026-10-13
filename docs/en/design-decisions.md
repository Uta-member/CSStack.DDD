# Design decisions

This page explains why CSStack.DDD is designed the way it is.

## Value objects do not validate in their constructors

**Decision:** Constructors check only the minimum conditions for the type to exist (such as non-null). Rules are reported by `Validate()`.

**Why:** Whether a value must be valid depends on the lifecycle of the entity. Values must be validated when an entity is created or changed, but not when it is reconstructed from persistence. If constructors rejected invalid values, data stored under older rules could not be loaded, and an aggregate with one invalid value could not be loaded even to fix that value.

**Trade-off:** The type alone does not guarantee validity. The guarantee comes from every write going through an operation of the domain.

## The entity owns the lifecycle

**Decision:** Entities provide separate factories for creation (with validation and events) and reconstruction (without either). Value objects have a single constructor.

**Why:** Creation and reconstruction are different moments in the life of an entity, not of a value. Putting both factories on value objects would spread the same distinction across every type.

## Validation returns errors; operations throw

**Decision:** `IValidatable.Validate()` returns a list of errors. Operations of entities throw `DomainValidationException`.

**Why:** Diagnosis needs to collect all errors, which exceptions cannot do. Enforcement needs to be impossible to ignore, which return values cannot guarantee. Neither mechanism alone satisfies both needs. Because enforcement happens only in operations of entities, missing enforcement is a bug in the behavior of an operation and can be found by its tests.

## Exceptions of the domain share a base class

**Decision:** `DomainValidationException` derives from the abstract class `DomainException`. Exceptions for business rules that are not about the input, such as "the requester cannot approve their own request", are defined by the application and also derive from `DomainException`. `ConcurrencyConflictException` does not derive from it.

**Why:** The upper layers need to handle every operation rejected by the domain in one place, for example by turning it into HTTP 400 or 422. Without a common base, each new exception would have to be added to that handling. Business rules that are not about the input are not reported as validation errors because there is no input to point to. `DomainException` is abstract so that each rule is represented by its own type, not by a message. A concurrency conflict is detected by the infrastructure, not rejected by a rule of the domain, so `ConcurrencyConflictException` is in the application layer.

## Errors are typed records, not codes

**Decision:** Each kind of error is a type derived from `ValidationError`, with typed properties.

**Why:** String codes with a dictionary of `object` arguments are hard to use correctly. Typed records can be handled with pattern matching, compared by value in tests and serialized with a type discriminator. A list of exceptions was also considered, but exceptions carry stack traces, messages and mutable data that a diagnosis does not need.

## Built-in errors for numbers are not generic

**Decision:** Values outside numeric bounds are reported with `BelowMinimum(decimal Minimum, decimal Actual)` and `AboveMaximum(decimal Maximum, decimal Actual)`. There is no generic range error.

**Why:** A generic error such as `OutOfRange<T>` cannot be matched without knowing `T`, and each closed type would have to be registered for serialization. `decimal` represents integers and decimal numbers exactly. A range with both bounds required cannot express a rule with only one bound, such as "1 or more", so each bound is its own error. The names differ from `TooLong` and `TooShort`, which are about length, so that the two are not confused. Other kinds of values, such as dates, are left to domain-specific errors.

## Errors have paths

**Decision:** `ValidationError.Path` is a dotted string, built by each level with `Under`.

**Why:** A value object does not know which property of which entity it is in. Each level adds only its own member name. A string is used instead of an array so that records keep equality by value and paths match the keys used by ASP.NET Core.

## Entity equality is enforced by a base class

**Decision:** `Entity<TSelf, TId>` implements equality by identifier and seals `Equals` and `GetHashCode`.

**Why:** An interface can require that equality is implemented, but not how. Only a base class can guarantee that entities are compared by identifier.

## Domain events are pulled on commit

**Decision:** Aggregates record events, and the infrastructure pulls them on commit through the non-generic `IDomainEventSource`.

**Why:** Dispatching immediately could publish changes that are later rolled back. Pulling on commit allows the events to be written to an outbox in the same transaction. Retrieving and clearing in one call prevents duplicate dispatch.

## Event handlers and dispatching are not provided

**Decision:** The toolkit provides recording and collecting events (`IDomainEvent`, `IDomainEventSource`) and nothing beyond. It does not provide a handler interface, a dispatcher, an outbox, retries or pipelines.

**Why:** How handlers are resolved, in which transaction they run and how they are retried depends on how the application is built: whether a scope is created per event or per handler, whether a message broker is used, and so on. A dispatcher that assumes one setup is unusable in applications that do not match it, and can lead them into the wrong setup. Libraries such as MediatR, Wolverine and MassTransit also have their own handler interfaces, which a toolkit interface would duplicate.

**Trade-off:** Each application has to provide, or choose a library for, the mechanism that resolves and invokes handlers from the type of an event.

## By default, reactions to events run in a transaction separate from the original change

**Decision:** The documentation recommends, as the default, that the unit of work writes events to the outbox in the same transaction as the aggregates, and that the outbox publisher reacts to them in a separate transaction. Reacting before commit, in the same transaction, is limited to cases where the business requires immediate consistency.

**Why:** If other aggregates are updated in the same transaction, a failure there rolls back the original change even though it succeeded. This contradicts the guideline of updating other aggregates with eventual consistency through domain events. Through the outbox, the original change and the record of its events are atomic, and failed reactions can be recovered by retrying.

**Trade-off:** The reaction's changes are applied later than the original change. Events are delivered at least once, so reactions must be idempotent.

## Repositories have `Save` instead of `Add` and `Update`

**Decision:** `IRepository` has `FindAsync` and `Save`. Insert or update is decided by the implementation.

**Why:** `Add` and `Update` expose a database concern (INSERT or UPDATE) to the caller. A collection-like repository with only `Add` and `Remove` requires change tracking, which ties the toolkit to ORMs such as EF Core. `Save` works with any data access technology.

## Deletion is optional

**Decision:** `Delete` is in a separate interface, `IDeletableRepository`.

**Why:** Many aggregates are never physically deleted. When deletion has a business meaning, such as withdrawal, it is a state transition of the aggregate.

## The domain has no `Version`

**Decision:** No `Version` property on aggregate roots. Concurrency tokens are kept in the infrastructure.

**Why:** A version is a persistence concern. The infrastructure can track the token of each loaded aggregate and detect conflicts without exposing it to the domain.

## Transactions are managed by the unit of work

**Decision:** Repositories do not take a transaction parameter. The application layer commits through `IUnitOfWork`.

**Why:** Passing transactions would put infrastructure types into domain interfaces and add a parameter to every method, and forgetting to pass one would silently run outside the transaction. The unit of work is also the natural place to collect domain events from every changed aggregate.

## What the toolkit does not provide

| Not provided | Reason |
|---|---|
| `IDomainService` | There are no members to require. Use naming conventions and architecture tests. |
| An execution context for the current user | It is an infrastructure concern. |
| A `Result` type | The toolkit uses exceptions for enforcement. |
| Command handlers or mediators | They are an architectural choice of the application, not part of DDD. |
| Domain event handlers and a dispatcher | How they are resolved and invoked depends on how the application is built. See [Event handlers and dispatching are not provided](#event-handlers-and-dispatching-are-not-provided). |
