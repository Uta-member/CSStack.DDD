# Getting started

This guide walks through the building blocks of CSStack.DDD by modeling a simple member registration.

## Installation

```shell
dotnet add package CSStack.DDD
```

CSStack.DDD targets .NET Standard 2.0, .NET 8 and .NET 10, so it can also be used from .NET Framework (4.7.2 or later is recommended). On .NET 8 and later, it is compatible with Native AOT and trimming because it uses no reflection or runtime code generation.

The examples in this documentation use records. To use records on .NET Framework, set `LangVersion` to `9.0` or later (for example, `latest`) in your project file, and define the following type in your project (or use a polyfill package such as PolySharp).

```csharp
namespace System.Runtime.CompilerServices
{
	internal static class IsExternalInit { }
}
```

## The two namespaces

| Namespace | Use from |
|---|---|
| `CSStack.DDD.Domain` | The domain layer: value objects, entities, aggregates, domain events, repository interfaces and validation |
| `CSStack.DDD.Application` | The application layer: the unit of work and the concurrency conflict exception |

If code in your domain layer has `using CSStack.DDD.Application;`, it is a sign that a responsibility has leaked across layers.

## Step 1: Value objects

A value object is defined by its attributes. Implementing it as a `record` gives you equality by attributes for free.

```csharp
public sealed record MemberId : ISingleValueObject<MemberId, Guid>
{
	public Guid Value { get; }

	public MemberId(Guid value)
	{
		Value = value;
	}

	public static MemberId New() => new(Guid.NewGuid());
}

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

Note that the constructor does not validate the format. It only checks what is needed for the type to exist (`null`). The rules are reported by `Validate()`, and the entity decides when to enforce them. See [Value objects](value-objects.md) for the reasoning.

## Step 2: Domain events

A domain event is an immutable record of something that has happened.

```csharp
public sealed record MemberRegistered(Guid EventId, DateTimeOffset OccurredAt, MemberId MemberId, Email Email) : IDomainEvent;
```

## Step 3: The aggregate root

```csharp
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
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		var member = new Member(MemberId.New(), email);
		member.AddDomainEvent(new MemberRegistered(Guid.NewGuid(), occurredAt, member.Id, email));
		return member;
	}

	public static Member Reconstruct(MemberId id, Email email) => new(id, email);

	public void ChangeEmail(Email email)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		Email = email;
	}
}
```

- `Register` creates a new member. It validates the input and raises an event.
- `Reconstruct` restores a member from persistence. It does not validate or raise events, so data stored under older rules can always be loaded and fixed.
- `ChangeEmail` is an operation. It validates the input before changing state.

Equality is already handled: two `Member` instances with the same `Id` are equal.

## Step 4: The repository

Define a repository interface per aggregate in the domain layer.

```csharp
public interface IMemberRepository : IRepository<Member, MemberId>
{
	Task<Member?> FindByEmailAsync(Email email, CancellationToken cancellationToken = default);
}
```

The implementation belongs to the infrastructure layer. See [Repositories and the unit of work](repositories-and-unit-of-work.md).

## Step 5: The use case

```csharp
public sealed class ChangeMemberEmailUseCase(IMemberRepository members, IUnitOfWork unitOfWork)
{
	public async Task ExecuteAsync(Guid memberId, string email, CancellationToken cancellationToken)
	{
		var member = await members.GetAsync(new MemberId(memberId), cancellationToken);

		member.ChangeEmail(new Email(email));
		members.Save(member);
		await unitOfWork.CommitAsync(cancellationToken);
	}
}
```

`GetAsync` is an extension method in `CSStack.DDD.Application`. It throws `AggregateNotFoundException` if the member does not exist. Use `FindAsync` instead when a missing aggregate is a normal result.

`Save` only registers the aggregate. `CommitAsync` writes all registered aggregates and their domain events in a single transaction (the events are written to the outbox). After the commit, the outbox publisher reacts to the events in a separate transaction.

## Step 6: Handle the exceptions

The toolkit uses these exceptions. Handle them once, globally, in the presentation layer.

| Exception | Meaning | Typical HTTP status |
|---|---|---|
| `DomainValidationException` | The input of an operation violates the rules. `Errors` contains all violations. | 400 |
| Other types derived from `DomainException` | A business rule that is not about the input rejected an operation. Define them in your domain. | 422 |
| `AggregateNotFoundException` | The aggregate requested by a use case does not exist. Thrown by `GetAsync`. | 404 |
| `ConcurrencyConflictException` | Another transaction modified the aggregate first. | 409 |

See [Validation](validation.md) for an ASP.NET Core example.

## Next steps

- [Value objects](value-objects.md)
- [Entities and aggregates](entities-and-aggregates.md)
- [Domain events](domain-events.md)
- [Validation](validation.md)
- [Repositories and the unit of work](repositories-and-unit-of-work.md)
- [Design decisions](design-decisions.md)
