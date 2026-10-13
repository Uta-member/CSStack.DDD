# Value objects

A value object is an immutable object defined only by its attributes. Two value objects with the same attributes are the same value.

## Interfaces

| Interface | Purpose |
|---|---|
| `IValueObject<TSelf>` | A value object. Requires `IEquatable<TSelf>`. Implement it as a `record` to also get the `==` and `!=` operators. |
| `ISingleValueObject<TSelf, TValue>` | A value object that wraps a single value (`Value`), such as an email address or an identifier. |

## Implementing with records

A C# `record` satisfies `IValueObject<TSelf>` automatically, because the compiler generates `Equals`, `GetHashCode`, `==` and `!=` based on all properties.

```csharp
public sealed record Address(PostalCode PostalCode, string City) : IValueObject<Address>;
```

> [!WARNING]
> A record compares arrays and collections such as `List<T>` **by reference**, not by content. If a value object contains a collection, override `Equals` and `GetHashCode` to compare the elements, or use a collection type that compares by value.

## Single-value objects and strongly typed identifiers

`ISingleValueObject<TSelf, TValue>` is useful for wrapping primitives so that the compiler can tell an email address from a name, or a member ID from an order ID.

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
```

Strongly typed identifiers satisfy the `TId : notnull, IEquatable<TId>` constraint of entities, so they can be used directly as entity identifiers.

## What to check in the constructor

**Check only the minimum conditions for the type to exist, such as non-null. Do not enforce business rules in the constructor.**

```csharp
public sealed record Email : ISingleValueObject<Email, string>, IValidatable
{
	public string Value { get; }

	public Email(string value)
	{
		Value = value ?? throw new ArgumentNullException(nameof(value)); // the type cannot exist without a value
	}

	public IReadOnlyList<ValidationError> Validate()
	{
		if (string.IsNullOrWhiteSpace(Value)) return [new Required()];
		if (!Value.Contains("@")) return [new InvalidFormat()];
		return [];
	}
}
```

The reason is persistence. If the constructor rejected invalid values:

- Data stored under older rules could no longer be loaded after the rules changed.
- A single invalid value would make the whole aggregate impossible to load, so not even the operation that fixes the value could run.
- The only way to repair the data would be to edit the database by hand.

Instead, the value object holds the **knowledge** of its rules in `Validate()`, and the **lifecycle of the entity** decides when to enforce them: on creation and on change, but not on reconstruction. See [Entities and aggregates](entities-and-aggregates.md) and [Validation](validation.md).

The trade-off is that the type alone no longer guarantees validity. The guarantee comes from the fact that every write goes through an operation of the domain, which validates its input.

## Value objects with multiple properties

When a value object contains other value objects, prefix the errors of each member with its name using `Under`.

```csharp
public sealed record Address(PostalCode PostalCode, Prefecture Prefecture) : IValueObject<Address>, IValidatable
{
	public IReadOnlyList<ValidationError> Validate() =>
	[
		.. PostalCode.Validate().Under(nameof(PostalCode)),
		.. Prefecture.Validate().Under(nameof(Prefecture)),
	];
}
```

A rule that spans several properties belongs to the value object as a whole. Report it with an empty path.

```csharp
public sealed record InvalidPeriod(DateTime Start, DateTime End) : ValidationError;

public sealed record Period(DateTime Start, DateTime End) : IValueObject<Period>, IValidatable
{
	public IReadOnlyList<ValidationError> Validate()
	{
		return Start > End ? [new InvalidPeriod(Start, End)] : [];
	}
}
```
