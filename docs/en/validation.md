# Validation

## Diagnosing and enforcing

CSStack.DDD separates validation into two roles.

| | Diagnosis | Gate |
|---|---|---|
| Who | `IValidatable.Validate()` | The operations of entities |
| What it does | Reports all problems | Rejects invalid changes |
| How | **Returns** errors | **Throws** `DomainValidationException` |
| Why | All errors can be collected and combined. An exception would stop at the first error. | The caller cannot ignore it. |

`Validate()` only reports. Enforcement happens in one place, the operations of the entity, so it can be verified by testing those operations.

## Validation errors

The kind of error is represented by its type, and the values that describe it are properties of the type. There are no string codes or dictionaries to parse.

| Type | Meaning |
|---|---|
| `Required` | A value is required but missing. |
| `InvalidFormat` | A value is not in the expected format. |
| `TooLong(MaxLength, ActualLength)` | A value is too long. |
| `TooShort(MinLength, ActualLength)` | A value is too short. |
| `OutOfRange<T>(Min, Max, Actual)` | A value is outside the allowed range. |

Define domain-specific errors by deriving from `ValidationError`.

```csharp
public sealed record UnderAge(int MinimumAge, int ActualAge) : ValidationError;
```

Errors are records, so they compare by value. This makes them easy to assert in tests: `Assert.Equal(new TooLong(20, 25) { Path = "Name" }, error)`.

## Paths

`ValidationError.Path` tells where the error occurred, such as `Address.PostalCode` or `Lines[2].Quantity`. An empty path means the object itself.

Each level adds only the name of its own member with `Under`. A value object does not know where it is used.

```
OrderLine.Quantity.Validate()    → TooLong { Path = "" }
  ↓ OrderLine adds .Under("Quantity")
OrderLine.Validate()             → TooLong { Path = "Quantity" }
  ↓ Order adds .Under("Lines[2]")
Order.Validate()                 → TooLong { Path = "Lines[2].Quantity" }
```

```csharp
public IReadOnlyList<ValidationError> Validate() =>
[
	.. ShippingAddress.Validate().Under(nameof(ShippingAddress)),
	.. DeliveryPeriod.Validate().Under(nameof(DeliveryPeriod)),
	.. Lines.SelectMany((line, i) => line.Validate().Under($"{nameof(Lines)}[{i}]")),
];
```

A rule that spans several properties (for example, "the start date must be before the end date") is reported with an empty path, as an error of the object as a whole.

## The gate

Use `ThrowIfInvalid` in the operations of entities.

```csharp
// A single input
public void ChangeShippingAddress(Address address)
{
	address.Validate().Under(nameof(ShippingAddress)).ThrowIfInvalid();
	ShippingAddress = address;
}

// Several inputs and business policies
public static Order Place(OrderId id, Address address, Period period, IReadOnlyList<OrderLine> lines)
{
	List<ValidationError> errors =
	[
		.. address.Validate().Under(nameof(ShippingAddress)),
		.. period.Validate().Under(nameof(DeliveryPeriod)),
		.. lines.SelectMany((line, i) => line.Validate().Under($"{nameof(Lines)}[{i}]")),
	];
	if (lines.Count == 0)
	{
		errors.Add(new Required { Path = nameof(Lines) });
	}
	errors.ThrowIfInvalid();

	return new Order(id, address, period, lines);
}
```

## Two kinds of rules

| | Rules of the value | Policies of the operation |
|---|---|---|
| Examples | An email address contains `@`; an amount is not negative | A name is at most 20 characters on registration; a member must be 18 or older |
| Changes over time | Rarely | When the business decides |
| Where | `Validate()` of the value object | The operation of the entity |

Both are enforced by the operations, not on reconstruction, so data stored under older rules can always be loaded.

## Turning errors into messages

Errors do not contain messages. Compose messages once, in the presentation layer.

```csharp
public sealed class ValidationMessageFormatter
{
	public string Format(ValidationError error) => error switch
	{
		Required => "This field is required.",
		InvalidFormat => "The format is invalid.",
		TooLong e => $"Must be {e.MaxLength} characters or fewer.",
		TooShort e => $"Must be {e.MinLength} characters or more.",
		_ => "The value is invalid.",
	};
}
```

The compiler does not check that a `switch` over a class hierarchy covers every type, so keep the `_` arm.

## ASP.NET Core example

Handle `DomainValidationException` once, globally. Controllers and use cases then need no error handling code.

```csharp
public sealed class DomainValidationExceptionHandler(ValidationMessageFormatter formatter) : IExceptionHandler
{
	public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
	{
		if (exception is not DomainValidationException validation)
		{
			return false;
		}

		var errors = validation.Errors
			.GroupBy(e => e.Path)
			.ToDictionary(g => g.Key, g => g.Select(formatter.Format).ToArray());

		context.Response.StatusCode = StatusCodes.Status400BadRequest;
		await context.Response.WriteAsJsonAsync(new ValidationProblemDetails(errors), cancellationToken);
		return true;
	}
}
```

The paths have the same shape as the keys of ASP.NET Core `ModelState`, so a client can map them to input fields.

## Validating input with the names of your form

The names of a form or request DTO may differ from the names in the domain (for example, `ZipCode` in the form and `Address.PostalCode` in the domain). Because value objects do not validate in their constructors, you can validate each field directly and add the name used by the form.

```csharp
List<ValidationError> errors =
[
	.. new Email(request.Email).Validate().Under(nameof(request.Email)),
	.. new PostalCode(request.ZipCode).Validate().Under(nameof(request.ZipCode)),
];
errors.ThrowIfInvalid();
```

The rules are still defined once, in the value objects. The gates in the domain remain as the final defense.
