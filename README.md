# CSStack.DDD

**English** | [日本語](https://github.com/Uta-member/CSStack.DDD/blob/master/README.ja.md) | [简体中文](https://github.com/Uta-member/CSStack.DDD/blob/master/README.zh-CN.md)

A lightweight toolkit of building blocks for Domain-Driven Design (DDD) in C#.

CSStack.DDD provides interfaces and base classes for value objects, entities, aggregate roots, domain events, repositories and the unit of work. It has no dependency on any persistence technology, so it works with EF Core, Dapper or any other data access library.

## Features

- **Value objects** with equality by attributes (`IValueObject`, `ISingleValueObject`)
- **Entities** with equality by identifier, enforced by a base class (`IEntity`, `Entity`)
- **Aggregate roots** that record domain events (`IAggregateRoot`, `AggregateRoot`)
- **Domain events** collected by the infrastructure on commit, ready for the outbox pattern (`IDomainEvent`, `IDomainEventSource`)
- **Validation** with typed errors and member paths such as `Address.PostalCode` (`IValidatable`, `ValidationError`)
- **Repositories** and a **unit of work** that keep transactions out of the domain (`IRepository`, `IDeletableRepository`, `IUnitOfWork`)

## Requirements

- .NET Standard 2.0 or later (.NET 8 or later, .NET Framework 4.7.2 or later, and so on)
- Compatible with Native AOT and trimming on .NET 8 or later
- C# 9 or later to implement value objects as records ([details](https://github.com/Uta-member/CSStack.DDD/blob/master/docs/en/getting-started.md#installation))

## Installation

```shell
dotnet add package CSStack.DDD
```

## Quick example

```csharp
using CSStack.DDD.Application;
using CSStack.DDD.Domain;

// A value object that wraps a single value
public sealed record Email : ISingleValueObject<Email, string>, IValidatable
{
	public string Value { get; }

	public Email(string value)
	{
		Value = value ?? throw new ArgumentNullException(nameof(value)); // only the minimum condition for the type to exist
	}

	public IReadOnlyList<ValidationError> Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) return [new Required()];
		if (!Value.Contains("@")) return [new InvalidFormat()];
		return [];
	}
}

// An aggregate root
public sealed class Member : AggregateRoot<Member, MemberId>
{
	public override MemberId Id { get; }
	public Email Email { get; private set; }

	private Member(MemberId id, Email email)
	{
		Id = id;
		Email = email;
	}

	// Creation: validates the input and raises an event
	public static Member Register(Email email, DateTimeOffset occurredAt)
	{
		email.Validate().Under(nameof(Email)).ThrowIfInvalid();
		var member = new Member(MemberId.New(), email);
		member.AddDomainEvent(new MemberRegistered(Guid.NewGuid(), occurredAt, member.Id, email));
		return member;
	}

	// Reconstruction from persistence: no validation, no events
	public static Member Reconstruct(MemberId id, Email email) => new(id, email);
}

// A use case in the application layer
public sealed class RegisterMemberUseCase(IRepository<Member, MemberId> members, IUnitOfWork unitOfWork)
{
	public async Task<MemberId> ExecuteAsync(string email, CancellationToken cancellationToken)
	{
		var member = Member.Register(new Email(email), DateTimeOffset.UtcNow);
		members.Save(member);                         // registers only
		await unitOfWork.CommitAsync(cancellationToken); // writes the aggregate and its events in one transaction
		return member.Id;
	}
}
```

## Documentation

| Topic | Description |
|---|---|
| [Getting started](https://github.com/Uta-member/CSStack.DDD/blob/master/docs/en/getting-started.md) | Installation and a walkthrough of the building blocks |
| [Value objects](https://github.com/Uta-member/CSStack.DDD/blob/master/docs/en/value-objects.md) | Equality, single-value objects and what to check in the constructor |
| [Entities and aggregates](https://github.com/Uta-member/CSStack.DDD/blob/master/docs/en/entities-and-aggregates.md) | Identity, lifecycle (creation and reconstruction) and operations |
| [Domain events](https://github.com/Uta-member/CSStack.DDD/blob/master/docs/en/domain-events.md) | Recording, collecting and dispatching events |
| [Validation](https://github.com/Uta-member/CSStack.DDD/blob/master/docs/en/validation.md) | Typed errors, paths, and diagnosing versus enforcing |
| [Repositories and the unit of work](https://github.com/Uta-member/CSStack.DDD/blob/master/docs/en/repositories-and-unit-of-work.md) | Persistence, transactions and concurrency |
| [Design decisions](https://github.com/Uta-member/CSStack.DDD/blob/master/docs/en/design-decisions.md) | Why the toolkit is designed the way it is |
| [Migrating from CSStack.TADA](https://github.com/Uta-member/CSStack.DDD/blob/master/docs/en/migrating-from-tada.md) | Moving a project from CSStack.TADA, the predecessor of this toolkit |

## Namespaces

| Namespace | Layer | Contents |
|---|---|---|
| `CSStack.DDD.Domain` | Domain | Value objects, entities, aggregates, domain events, repositories, validation |
| `CSStack.DDD.Application` | Application | Unit of work, concurrency conflict exception |

## License

This project is licensed under the [MIT License](https://github.com/Uta-member/CSStack.DDD/blob/master/LICENSE.txt).
